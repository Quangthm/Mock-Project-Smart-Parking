using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Models.JwT;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;
public sealed class WorkflowFeatureTests
{
    private sealed class Transport:IWorkflowTransport
    {
        public bool Fail;public List<WorkflowMessage> Sent=[];
        public Task SendAsync(WorkflowMessage message,Guid id,CancellationToken ct){if(Fail)throw new IOException("provider unavailable");Sent.Add(message);return Task.CompletedTask;}
    }
    private sealed class Otp:IOtpSender
    {public string Code="";public Task SendAsync(string channel,string contact,string code,CancellationToken ct){Code=code;return Task.CompletedTask;}}
    private static async Task Database(Func<DbContextOptions<AppDbContext>,Task> test)
    {
        var input=Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var name="smartpark_auth_test_"+Guid.NewGuid().ToString("N");await using var admin=new NpgsqlConnection(input);await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin).ExecuteNonQueryAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(input){Database=name}.ConnectionString).Options;
        try{await using(var db=new AppDbContext(options)){await ServiceSchema.InitializeAsync(db);await ServiceSchema.ApplyFeaturesAsync(db);db.Roles.Add(new(){Code="PLATFORM_ADMIN",Name="Platform Admin"});await db.SaveChangesAsync();}await test(options);}
        finally{NpgsqlConnection.ClearAllPools();await new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)",admin).ExecuteNonQueryAsync();}
    }
    private static AccountWorkflowService Workflow(AppDbContext db,TimeProvider clock)=>WorkflowTestSupport.Create(db,clock);
    private static async Task<string> Code(AppDbContext db,Guid challenge)
    {
        var delivery=await db.WorkflowDeliveries.OrderByDescending(d=>d.CreatedAt).FirstAsync(d=>d.ChallengeId==challenge && d.Status=="pending");
        var message=JsonSerializer.Deserialize<WorkflowMessage>(new TestWorkflowProtector().Unprotect(delivery.ProtectedPayload))!;
        return Regex.Match(message.Body,@"\d{6}").Value;
    }
    private static User Identity(string role,string email)=>new(){Id=Guid.NewGuid(),FullName=role,Email=email,Status=UserStatus.Active,
        Accounts=[new(){Id=Guid.NewGuid(),AccountRoles=[new(){RoleCode=role}]}]};
    [PostgresFact]
    public async Task OwnerRequiresBothContactsActionPermissionAndIndependentRetryableDelivery()=>await Database(async options=>
    {
        var clock=new TestClock();var hash=new BcryptPasswordService();var directory=new TestParkingDirectory();var admin=Identity("PLATFORM_ADMIN","admin@test.example");
        Guid applicationId,ownerId;ChallengeResult email,phone;
        await using(var db=new AppDbContext(options))
        {
            db.Users.Add(admin);await db.SaveChangesAsync();var workflows=Workflow(db,clock);var owners=new OwnerRegistrationService(db,hash,clock,directory,workflows);
            var app=await owners.RegisterAsync(new(){FullName="Owner",BusinessName="Company",Email="owner@gmail.com",Phone="+84912345678",LotType="outdoor",AgreedToPolicy=true},default);
            applicationId=app.Id;ownerId=app.OwnerId;email=app.Verification!;phone=app.PhoneVerification!;
            Assert.False(app.ContactVerified);Assert.Equal("0912345678",app.Phone);
            Assert.NotEqual("",(await db.WorkflowDeliveries.FirstAsync()).ProtectedPayload);
            await Assert.ThrowsAsync<AuthException>(()=>owners.ReviewAsync(admin.Id!.Value,app.Id,new(){Status="approved"},default));
        }
        async Task Verify(ChallengeResult c){await using var db=new AppDbContext(options);await Workflow(db,clock).VerifyOwnerAsync(c.ChallengeId,await Code(db,c.ChallengeId),default);}
        await Verify(email);
        await using(var db=new AppDbContext(options))Assert.Equal(UserStatus.PendingVerification,(await db.Users.FindAsync(ownerId))!.Status);
        await Verify(phone);
        await using(var db=new AppDbContext(options))
        {
            var account=await db.Accounts.SingleAsync(a=>a.UserId==admin.Id);account.Permissions=[];await db.SaveChangesAsync();
            var owners=new OwnerRegistrationService(db,hash,clock,directory,Workflow(db,clock));
            Assert.Equal("FORBIDDEN",(await Assert.ThrowsAsync<AuthException>(()=>owners.ReviewAsync(admin.Id!.Value,applicationId,new(){Status="approved"},default))).Code);
            account.Permissions=null;await db.SaveChangesAsync();
        }
        var outcomes=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{await using var db=new AppDbContext(options);try{await new OwnerRegistrationService(db,hash,clock,directory,Workflow(db,clock)).ReviewAsync(admin.Id!.Value,applicationId,new(){Status="approved"},default);return "approved";}catch(AuthException e){return e.Code;}}));
        Assert.Single(outcomes,x=>x=="approved");Assert.Single(outcomes,x=>x=="APPLICATION_CLOSED");
        await using(var db=new AppDbContext(options))
        {
            var workflow=Workflow(db,clock);var transport=new Transport{Fail=true};
            for(var i=0;i<3;i++)await workflow.DeliverOneAsync(transport,default);
            var delivery=await db.WorkflowDeliveries.SingleAsync(d=>d.Kind=="OWNER_DECISION");Assert.Equal("failed",delivery.Status);
            Assert.Equal(UserStatus.Active,(await db.Users.FindAsync(ownerId))!.Status);Assert.Single(await db.OwnerApplications.ToArrayAsync());
            await workflow.DeliveryAsync(admin.Id!.Value,delivery.Id,true,default);transport.Fail=false;await workflow.DeliverOneAsync(transport,default);
            Assert.Equal("sent",delivery.Status);Assert.Equal("",delivery.ProtectedPayload);Assert.Single(transport.Sent);
        }
    });
    [PostgresFact]
    public async Task PasswordlessDriverOtpLoginMfaReplayAndContactIsolation()=>await Database(async options=>
    {
        var clock=new TestClock();var password=new BcryptPasswordService();var sender=new Otp();Guid driver;ChallengeResult challenge;string otp;
        await using(var db=new AppDbContext(options))
        {
            var registrations=new DriverRegistrationService(db,password,sender,clock);
            var result=await registrations.RegisterAsync(new(){FullName="Driver",Email="driver@sample.test",Phone="+84911223344"},default);
            var recovered=await registrations.RecoverAsync("driver@sample.test",default);Assert.Equal(result.RegistrationId,recovered.RegistrationId);
            await registrations.VerifyAsync(new(){RegistrationId=result.RegistrationId,Code=sender.Code},default);
            driver=(await db.Users.SingleAsync()).Id!.Value;var workflow=Workflow(db,clock);
            Assert.Equal("CONTACT_UNVERIFIED",(await Assert.ThrowsAsync<AuthException>(()=>workflow.RequestLoginAsync("0911223344",default))).Code);
            challenge=await workflow.RequestLoginAsync("DRIVER@SAMPLE.TEST",default);otp=await Code(db,challenge.ChallengeId);
        }
        var jwt=new JwtOptions{Issuer="workflow-tests",Audience="workflow-tests"};using var keys=new JwtKeyProvider(jwt,true);
        async Task<UserSessionDto> Login(string code,string? totp=null){await using var db=new AppDbContext(options);var workflow=Workflow(db,clock);var store=new PostgresAuthSessionStore(db,clock);var sessions=new AuthSessionService(new AccessTokenService(jwt,keys,store,clock),store,new(),clock);return await workflow.CompleteLoginAsync(new(){ChallengeId=challenge.ChallengeId,Code=code,Totp=totp},sessions,default);}
        var session=await Login(otp);Assert.Equal(86400,session.ExpiresIn);
        Assert.Equal("CHALLENGE_CLOSED",(await Assert.ThrowsAsync<AuthException>(()=>Login(otp))).Code);
        string secret;
        await using(var db=new AppDbContext(options))
        {
            var workflow=Workflow(db,clock);var setup=JsonSerializer.SerializeToElement(await workflow.SetupMfaAsync(driver,default));secret=setup.GetProperty("secret").GetString()!;
            await workflow.EnableMfaAsync(driver,AccountWorkflowService.Totp(secret,clock.Now.ToUnixTimeSeconds()/30),default);
            clock.Now=clock.Now.AddSeconds(31);challenge=await workflow.RequestLoginAsync("driver@sample.test",default);otp=await Code(db,challenge.ChallengeId);
        }
        Assert.Equal("MFA_REQUIRED",(await Assert.ThrowsAsync<AuthException>(()=>Login(otp))).Code);
        await Login(otp,AccountWorkflowService.Totp(secret,clock.Now.ToUnixTimeSeconds()/30));
        await using(var db=new AppDbContext(options))
        {
            var driverUser=await db.Users.FindAsync(driver);Assert.Equal("",driverUser!.PasswordHash);
            var workflow=Workflow(db,clock);Assert.True(await workflow.MfaEnabledAsync(driver,default));
            driverUser.PasswordHash=password.Hash("Password@123");await db.SaveChangesAsync();
            var store=new PostgresAuthSessionStore(db,clock);var policy=new AuthenticationPolicy();
            var handler=new global::UserService.Application.Usecase.Login.LoginCommandHandler(new UnitOfWork(db,new UserRepository(db)),password,
                new AuthSessionService(new AccessTokenService(jwt,keys,store,clock),store,policy,clock),policy,clock,workflow);
            var bypass=await handler.Handle(new(){Email="driver@sample.test",Password="Password@123"},default);
            Assert.Equal("MFA_REQUIRED",bypass.ErrorCode);Assert.False(bypass.IsSuccess);
        }
    });
    [PostgresFact]
    public async Task BootstrapTokenSingleUsePasswordHashSessionRevocationAndDeliveryScope()=>await Database(async options=>
    {
        var clock=new TestClock();var password=new BcryptPasswordService();var owner=Identity("BUSINESS_OWNER","owner@test.example");owner.Accounts.Single().TenantId=Guid.NewGuid();var other=Identity("BUSINESS_OWNER","other@test.example");var directory=new TestParkingDirectory();var site=Guid.NewGuid();directory.Sites.Add(site,owner.Accounts.Single().TenantId!.Value);
        Guid operatorId,challengeId,deliveryId;string token;
        await using(var db=new AppDbContext(options))
        {
            db.Users.AddRange(owner,other);await db.SaveChangesAsync();var workflow=Workflow(db,clock);var service=new OperatorProvisioningService(db,password,clock,directory,workflow);
            var created=await service.CreateAsync(owner.Id!.Value,new(){FullName="Staff",Email="staff@test.example",Password="Password@123",SiteIds=[site],Permissions=["CASH_COLLECT"]},default);operatorId=created.Id;deliveryId=created.DeliveryId!.Value;
            var challenge=await db.AuthChallenges.SingleAsync(c=>c.UserId==operatorId);challengeId=challenge.Id;
            var delivery=await db.WorkflowDeliveries.SingleAsync(d=>d.Id==deliveryId);var body=JsonSerializer.Deserialize<WorkflowMessage>(new TestWorkflowProtector().Unprotect(delivery.ProtectedPayload))!.Body;token=Regex.Match(body,@"token=([A-F0-9]+)").Groups[1].Value;
            Assert.DoesNotContain(token,delivery.ProtectedPayload);Assert.DoesNotContain("Password@123",delivery.ProtectedPayload);
            await Assert.ThrowsAsync<AuthException>(()=>workflow.DeliveryAsync(other.Id!.Value,deliveryId,true,default));
            db.AuthSessions.Add(new(){Id=Guid.NewGuid(),UserId=operatorId,AccessTokenId=Guid.NewGuid(),TokenHash="test",CreatedAt=clock.Now,ExpiresAt=clock.Now.AddDays(7),AccessExpiresAt=clock.Now.AddDays(1)});await db.SaveChangesAsync();
            Assert.Equal("INVALID_TOKEN",(await Assert.ThrowsAsync<AuthException>(()=>workflow.ChangeBootstrapPasswordAsync(new(){ChallengeId=challengeId,Token=token+"A",Password="Changed@123"},default))).Code);
            Assert.Equal("CHALLENGE_CLOSED",(await Assert.ThrowsAsync<AuthException>(()=>workflow.VerifyOwnerAsync(challengeId,"000000",default))).Code);
            await workflow.ChangeBootstrapPasswordAsync(new(){ChallengeId=challengeId,Token=token,Password="Changed@123"},default);
            Assert.True(password.Verify("Changed@123",(await db.Users.FindAsync(operatorId))!.PasswordHash));Assert.False(password.Verify("Password@123",(await db.Users.FindAsync(operatorId))!.PasswordHash));Assert.True((await db.AuthSessions.AsNoTracking().SingleAsync()).IsRevoked);
            await Assert.ThrowsAsync<AuthException>(()=>workflow.ChangeBootstrapPasswordAsync(new(){ChallengeId=challengeId,Token=token,Password="Changed@456"},default));
            Assert.Single(await db.Users.Where(u=>u.Id==operatorId).ToArrayAsync());Assert.Single(await db.OperatorGrants.ToArrayAsync());
            var user=await db.Users.FindAsync(operatorId);await workflow.QueueOnboardingAsync(user!,owner.Id.Value,"Changed@123",default);
            var expired=await db.AuthChallenges.SingleAsync(c=>c.UserId==operatorId && c.ConsumedAt==null);
            clock.Now=expired.ExpiresAt;
            Assert.Equal("INVALID_TOKEN",(await Assert.ThrowsAsync<AuthException>(()=>workflow.ChangeBootstrapPasswordAsync(new(){ChallengeId=expired.Id,Token=token,Password="Changed@456"},default))).Code);
            Assert.True(password.Verify("Changed@123",user!.PasswordHash));
        }
    });
    [PostgresFact]
    public async Task ContactMigrationDoesNotInferVerificationOrMergeConflictingAccounts()=>await Database(async options=>
    {
        await using var db=new AppDbContext(options);var a=Identity("DRIVER"," LEGACY@TEST.EXAMPLE ");a.Phone="+84911111222";
        db.Users.Add(a);await db.SaveChangesAsync();await ServiceSchema.ApplyFeaturesAsync(db);db.ChangeTracker.Clear();
        var normalized=await db.Users.SingleAsync();Assert.Equal("legacy@test.example",normalized.Email);Assert.Equal("0911111222",normalized.Phone);
        Assert.Null(normalized.EmailVerifiedAt);Assert.Null(normalized.PhoneVerifiedAt);
        var b=Identity("DRIVER","other@test.example");b.Phone="+84911111222";db.Users.Add(b);await db.SaveChangesAsync();
        var conflict=await Assert.ThrowsAsync<PostgresException>(()=>ServiceSchema.ApplyFeaturesAsync(db));Assert.Equal("23505",conflict.SqlState);
        await db.Database.ExecuteSqlRawAsync("ROLLBACK");db.ChangeTracker.Clear();
        Assert.Equal(2,await db.Users.CountAsync());Assert.Equal("+84911111222",(await db.Users.FindAsync(b.Id))!.Phone);
    });
    [PostgresFact]
    public async Task OwnerOtpResendPreservesFailuresLocksAndExactExpiry()=>await Database(async options=>
    {
        var clock=new TestClock();Guid email;string oldCode;
        await using(var db=new AppDbContext(options))
        {
            var workflow=Workflow(db,clock);var service=new OwnerRegistrationService(db,new BcryptPasswordService(),clock,new TestParkingDirectory(),workflow);
            var app=await service.RegisterAsync(new(){FullName="Owner",BusinessName="Company",Email="boundary@gmail.com",Phone="0912222333",LotType="outdoor",AgreedToPolicy=true},default);
            email=app.Verification!.ChallengeId;oldCode=await Code(db,email);
        }
        async Task<string> Wrong(string code){await using var db=new AppDbContext(options);return(await Assert.ThrowsAsync<AuthException>(()=>Workflow(db,clock).VerifyOwnerAsync(email,code,default))).Code;}
        async Task<ChallengeResult> Resend(){await using var db=new AppDbContext(options);return await Workflow(db,clock).ResendOwnerAsync(email,default);}
        Assert.Equal("OTP_COOLDOWN",(await Assert.ThrowsAsync<AuthException>(()=>Resend())).Code);
        Assert.Equal("OTP_INVALID",await Wrong(oldCode=="000000"?"000001":"000000"));
        clock.Now=clock.Now.AddSeconds(60);await Resend();
        Assert.Equal("OTP_INVALID",await Wrong(oldCode));
        await using(var db=new AppDbContext(options))
        {
            Assert.Equal(2,(await db.AuthChallenges.FindAsync(email))!.Attempts);
            Assert.Single(await db.WorkflowDeliveries.Where(d=>d.ChallengeId==email && d.Status=="pending").ToArrayAsync());
            Assert.All(await db.WorkflowDeliveries.Where(d=>d.ChallengeId==email && d.Status=="cancelled").ToArrayAsync(),d=>Assert.Equal("",d.ProtectedPayload));
        }
        Assert.Equal("OTP_LOCKED",await Wrong(oldCode));
        Assert.Equal("OTP_LOCKED",(await Assert.ThrowsAsync<AuthException>(()=>Resend())).Code);
        clock.Now=clock.Now.AddMinutes(15);var renewed=await Resend();
        clock.Now=renewed.ExpiresAt;
        await using(var db=new AppDbContext(options))
        {
            Assert.Equal("OTP_EXPIRED",(await Assert.ThrowsAsync<AuthException>(()=>Workflow(db,clock).VerifyOwnerAsync(email,"000000",default))).Code);
            Assert.Equal(UserStatus.PendingVerification,(await db.Users.SingleAsync()).Status);Assert.Empty(await db.AuthSessions.ToArrayAsync());
        }
    });
    [PostgresFact]
    public async Task VehicleRawCanonicalPersistenceCrossAccountAndOpenOwnershipBoundary()=>await Database(async options=>
    {
        var clock=new TestClock();var a=Identity("DRIVER","a@test.example");var b=Identity("DRIVER","b@test.example");
        await using var db=new AppDbContext(options);db.Users.AddRange(a,b);await db.SaveChangesAsync();var vehicles=new VehicleRegistrationService(db,clock);
        var first=await vehicles.SaveAsync(a.Id!.Value,null,new(){Plate=" 51a-123.45 ",VehicleType="CAR",ImageReference="https://images.example.test/car.jpg"},default);Assert.Equal(" 51a-123.45 ",first.RawPlate);Assert.Equal("51A12345",first.CanonicalPlate);
        Assert.Equal("VEHICLE_NOT_FOUND",(await Assert.ThrowsAsync<AuthException>(()=>vehicles.SaveAsync(b.Id!.Value,first.Id,new(){Plate="51A12345",VehicleType="CAR",ImageReference="https://images.example.test/car.jpg"},default))).Code);
        Assert.Empty(await vehicles.ListAsync(b.Id!.Value,default));
        await vehicles.SaveAsync(b.Id!.Value,null,new(){Plate="51A12345",VehicleType="CAR",ImageReference="https://images.example.test/car.jpg"},default); // No invented FR-VEH-02 binding/claim policy.
        await Assert.ThrowsAsync<AuthException>(()=>vehicles.SaveAsync(a.Id!.Value,null,new(){Plate="NO PLATE",VehicleType="CAR",ImageReference="https://images.example.test/car.jpg"},default));
        await Assert.ThrowsAsync<AuthException>(()=>vehicles.SaveAsync(a.Id!.Value,null,new(){Plate="59X112345",VehicleType="BICYCLE",ImageReference="https://images.example.test/car.jpg"},default));
        Assert.Equal("VALIDATION_FAILED",(await Assert.ThrowsAsync<AuthException>(()=>vehicles.SaveAsync(a.Id!.Value,null,new(){Plate="51A12345",VehicleType="CAR"},default))).Code);
        Assert.Single(await vehicles.ListAsync(a.Id!.Value,default));
        await using var reloaded=new AppDbContext(options);Assert.Equal("51A12345",(await new VehicleRegistrationService(reloaded,clock).ListAsync(a.Id.Value,default)).Single().CanonicalPlate);
    });
    [Theory]
    [InlineData("51a-123.45","CAR","51A12345")]
    [InlineData("59-x1 123.45","MOTORCYCLE","59X112345")]
    public void PlateNormalizationIsDeterministic(string raw,string type,string expected)=>Assert.Equal(expected,VehicleRegistrationService.NormalizePlate(raw,type));
    [Fact]
    public void TotpMatchesPublishedAlgorithmVector()=>Assert.Equal("287082",AccountWorkflowService.Totp("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",1));
}
