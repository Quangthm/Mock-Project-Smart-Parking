using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartParking.ParkingService.API;
using SmartParking.ParkingService.Infrastructure;
using SmartParking.ParkingService.Application;
using SmartParking.ReservationService.API;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.API;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

public sealed class DatabaseIntegrationTests
{
    [PostgresFact]
    public async Task RealHostsSeparateDatabasesOwnerOnboardingOperatorAndRevocation()
    {
        var input=Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var names=Enumerable.Range(0,3).Select(_=>"smartpark_auth_test_"+Guid.NewGuid().ToString("N")).ToArray();
        await using var admin=new NpgsqlConnection(input);await admin.OpenAsync();
        foreach(var name in names) await new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin).ExecuteNonQueryAsync();
        var connections=names.Select(name=>new NpgsqlConnectionStringBuilder(input){Database=name}.ConnectionString).ToArray();
        try
        {
            var files=new[]{("microservices/01-user-service-db.sql","integration/01-user-service-alignment.sql"),("microservices/02-parking-service-db.sql","integration/02-parking-service-alignment.sql"),("microservices/03-reservation-session-service-db.sql","integration/03-reservation-structure-safety.sql")};
            for(var i=0;i<3;i++)
            {
                await using var db=new NpgsqlConnection(connections[i]);await db.OpenAsync();
                foreach(var file in new[]{files[i].Item1,files[i].Item2}) await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(ServiceSchema.Root,"scripts/database",file)),db).ExecuteNonQueryAsync();
                if(i==1){var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"SmartParking.slnx")))root=root.Parent;await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root!.FullName,"scripts/database/05.9-Parking-Backup-Operations.sql")),db).ExecuteNonQueryAsync();}
                if(i==2){var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!File.Exists(Path.Combine(root.FullName,"SmartParking.slnx")))root=root.Parent;await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root!.FullName,"scripts/database/05.10-Reservation-Confirmation-Time.sql")),db).ExecuteNonQueryAsync();}
            }
            const string key="integration-test-service-key-32-characters";
            void Config(WebApplicationBuilder b,string name,string connection)
            {
                b.Logging.ClearProviders();b.WebHost.UseTestServer();
                b.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{[$"ConnectionStrings:{name}"]=connection,["Services:Key"]=key,["Services:User"]="http://user.test/",["Services:Parking"]="http://parking.test/",["Services:Reservation"]="http://reservation.test/"});
            }
            await using var reservations=ReservationHost.Build([],b=>Config(b,"Reservation",connections[2]));await reservations.StartAsync();
            WebApplication? userHost=null;
            await using var parking=ParkingHost.Build([],b=>
            {
                Config(b,"Parking",connections[1]);
                b.Services.AddHttpClient<OwnerIdentityClient>().ConfigurePrimaryHttpMessageHandler(()=>userHost!.GetTestServer().CreateHandler());
                b.Services.AddHttpClient<SlotOperatorClient>().ConfigurePrimaryHttpMessageHandler(()=>userHost!.GetTestServer().CreateHandler());
                b.Services.AddHttpClient<IStructureCommitments,StructureCommitmentsClient>().ConfigurePrimaryHttpMessageHandler(()=>reservations.GetTestServer().CreateHandler());
                b.Services.AddHttpClient<IStructureImpactPlanner,StructureImpactPlanner>().ConfigurePrimaryHttpMessageHandler(()=>reservations.GetTestServer().CreateHandler());
            });await parking.StartAsync();
            await using var users=await UserHost.BuildAsync([],b=>
            {
                Config(b,"User",connections[0]);b.Environment.EnvironmentName="Development";
                b.Services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
                b.Services.AddHttpClient<IParkingDirectory,ParkingDirectoryClient>().ConfigurePrimaryHttpMessageHandler(()=>parking.GetTestServer().CreateHandler());
            });userHost=users;await users.StartAsync();
            await using(var scope=users.Services.CreateAsyncScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await ServiceSchema.ApplyFeaturesAsync(db);
                var bootstrap=scope.ServiceProvider.GetRequiredService<DataSeeder>();
                await Assert.ThrowsAsync<InvalidOperationException>(()=>bootstrap.SeedAdminAsync("admin@integration.test","weak"));
                await bootstrap.SeedAdminAsync("admin@integration.test","Password@123");
                await bootstrap.SeedAdminAsync("admin@integration.test","Password@123");
                Assert.Equal(1,await db.Users.CountAsync(u=>u.Email=="admin@integration.test"));
            }
            using var adminClient=users.GetTestClient();using var owner=users.GetTestClient();using var lots=parking.GetTestClient();using var staff=users.GetTestClient();
            async Task<JsonElement> Data(HttpResponseMessage r,HttpStatusCode expected=HttpStatusCode.OK)
            { var body=await r.Content.ReadAsStringAsync();Assert.True(r.StatusCode==expected,$"Expected {expected}; received {r.StatusCode}: {body}");return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone(); }
            async Task<string> Login(HttpClient client,string email,string password="Password@123")
            { var result=await Data(await client.PostAsJsonAsync("/api/auth/login",new{email,password}));var token=result.GetProperty("accessToken").GetString()!;client.DefaultRequestHeaders.Authorization=new("Bearer",token);Assert.Equal(86400,result.GetProperty("expiresIn").GetInt32());return token; }
            var registration=await Data(await owner.PostAsJsonAsync("/api/auth/register/owner",new{fullName="Owner",businessName="Integrated Parking",email="owner@integration.test",phone="0912345678",password="Password@123",lotType="outdoor",agreedToPolicy=true}),HttpStatusCode.Created);
            await using(var scope=users.Services.CreateAsyncScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                foreach(var name in new[]{"verification","phoneVerification"})
                {
                    var challenge=registration.GetProperty(name).GetProperty("challengeId").GetGuid();
                    var delivery=await db.WorkflowDeliveries.SingleAsync(d=>d.ChallengeId==challenge);
                    var message=JsonSerializer.Deserialize<WorkflowMessage>(scope.ServiceProvider.GetRequiredService<IWorkflowProtector>().Unprotect(delivery.ProtectedPayload))!;
                    Assert.Equal(HttpStatusCode.OK,(await owner.PostAsJsonAsync("/api/auth/register/owner/verify",new{challengeId=challenge,code=System.Text.RegularExpressions.Regex.Match(message.Body,@"\d{6}").Value})).StatusCode);
                }
            }
            Assert.Equal(HttpStatusCode.Unauthorized,(await owner.PostAsJsonAsync("/api/auth/login",new{email="owner@integration.test",password="Password@123"})).StatusCode);
            await Login(adminClient,"admin@integration.test");
            await Data(await adminClient.PatchAsJsonAsync($"/api/owner-applications/{registration.GetProperty("id").GetGuid()}/review",new{status="approved"}));
            var ownerToken=await Login(owner,"owner@integration.test");lots.DefaultRequestHeaders.Authorization=new("Bearer",ownerToken);
            Assert.Equal(HttpStatusCode.Unauthorized,(await parking.GetTestClient().PutAsJsonAsync($"/internal/tenants/{Guid.NewGuid()}",new{name="Forged",email="x",phone="x"})).StatusCode);
            var site=await Data(await lots.PostAsJsonAsync("/api/parking-lots",new{code="S1",name="First site",address="Address"}),HttpStatusCode.Created);var siteId=site.GetProperty("id").GetGuid();
            var unit=await Data(await lots.PostAsJsonAsync($"/api/parking-lots/{siteId}/units",new{type="ZONE",name="Outdoor",capacity=10}));
            await Data(await lots.PostAsJsonAsync($"/api/parking-lots/{siteId}/slots",new{unitId=unit.GetGuid(),code="M1",vehicleType="MOTORCYCLE",type="STANDARD"}));
            var layout=await Data(await lots.GetAsync($"/api/parking-lots/{siteId}/structure"));Assert.Equal(1,layout.GetProperty("slots").GetArrayLength());
            var slotId=layout.GetProperty("slots")[0].GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Forbidden,(await lots.PatchAsJsonAsync($"/api/parking-lots/{siteId}/structure",new{action="markBackup",resourceId=slotId,active=true})).StatusCode);
            using(var backupResponse=await lots.PatchAsJsonAsync($"/api/parking-lots/{siteId}/structure",new{action="backup",capacity=1,vehicleType="MOTORCYCLE"}))
            {Assert.Equal(HttpStatusCode.OK,backupResponse.StatusCode);Assert.True(JsonDocument.Parse(await backupResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("success").GetBoolean());}
            layout=await Data(await lots.GetAsync($"/api/parking-lots/{siteId}/structure"));
            Assert.Contains(layout.GetProperty("capacityViews").EnumerateArray(),v=>v.GetProperty("unitId").ValueKind==JsonValueKind.Null && v.GetProperty("vehicleType").GetString()=="MOTORCYCLE" && v.GetProperty("effectiveBackup").GetInt32()==1);
            await using(var scope=users.Services.CreateAsyncScope())
            {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var account=await db.Accounts.SingleAsync(a=>a.UserId==registration.GetProperty("ownerId").GetGuid());account.Permissions=[];await db.SaveChangesAsync();}
            Assert.Equal(HttpStatusCode.Forbidden,(await lots.GetAsync("/api/parking-lots")).StatusCode);
            await using(var scope=users.Services.CreateAsyncScope())
            {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var account=await db.Accounts.SingleAsync(a=>a.UserId==registration.GetProperty("ownerId").GetGuid());account.Permissions=null;await db.SaveChangesAsync();}
            var created=await Data(await owner.PostAsJsonAsync("/api/users",new{fullName="Staff",email="staff@integration.test",password="Password@123",siteIds=new[]{siteId},permissions=new[]{"DEVICE_STATUS_VIEW"}}),HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsJsonAsync("/api/users",new{fullName="Foreign Staff",email="foreign@integration.test",password="Password@123",siteIds=new[]{Guid.NewGuid()},permissions=new[]{"DEVICE_STATUS_VIEW"}})).StatusCode);
            await Login(staff,"staff@integration.test");Assert.Equal(HttpStatusCode.OK,(await staff.GetAsync("/api/auth/me")).StatusCode);
            var assigned=await Data(await staff.GetAsync("/api/operators/me"));Assert.Equal(siteId,assigned[0].GetProperty("siteId").GetGuid());
            Assert.Equal("DEVICE_STATUS_VIEW",assigned[0].GetProperty("permissions")[0].GetString());
            Assert.Single((await Data(await owner.GetAsync("/api/operators"))).EnumerateArray());
            using var operational=parking.GetTestClient();operational.DefaultRequestHeaders.Authorization=staff.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.Forbidden,(await operational.PostAsJsonAsync($"/api/parking-lots/{siteId}/slots/{slotId}/backup",new{reason="Preserve reservation backup"})).StatusCode);
            await using(var scope=users.Services.CreateAsyncScope())
            {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var grant=await db.OperatorGrants.SingleAsync();grant.Permissions=["DEVICE_STATUS_VIEW","SLOT_OVERRIDE"];await db.SaveChangesAsync();}
            Assert.Equal(HttpStatusCode.OK,(await operational.PostAsJsonAsync($"/api/parking-lots/{siteId}/slots/{slotId}/backup",new{reason="Preserve reservation backup"})).StatusCode);
            var operationalLayout=await Data(await operational.GetAsync($"/api/parking-lots/{siteId}/operational-layout"));
            Assert.Equal("BACKUP",operationalLayout.GetProperty("slots")[0].GetProperty("reservationState").GetString());
            Assert.Equal("OPERATIONAL",operationalLayout.GetProperty("slots")[0].GetProperty("operationalStatus").GetString());
            Assert.Equal(HttpStatusCode.Forbidden,(await operational.GetAsync($"/api/parking-lots/{Guid.NewGuid()}/operational-layout")).StatusCode);
            await using(var audit=new NpgsqlConnection(connections[1]))
            {await audit.OpenAsync();await using var check=new NpgsqlCommand("SELECT count(*) FROM structure_slot_audit WHERE actor_id=$1 AND reason='Preserve reservation backup'",audit);check.Parameters.AddWithValue(created.GetProperty("id").GetGuid());Assert.Equal(1L,(long)(await check.ExecuteScalarAsync())!);}
            await using(var scope=users.Services.CreateAsyncScope())
            {
                var rights=scope.ServiceProvider.GetRequiredService<IOperatorProvisioningService>();
                Assert.True(await rights.HasPermissionAsync(created.GetProperty("id").GetGuid(),siteId,"DEVICE_STATUS_VIEW",default));
                Assert.False(await rights.HasPermissionAsync(created.GetProperty("id").GetGuid(),siteId,"CASH_COLLECT",default));
            }
            await Data(await adminClient.PatchAsJsonAsync($"/api/users/{created.GetProperty("id").GetGuid()}/status",new{status="locked"}));
            Assert.Equal(HttpStatusCode.Unauthorized,(await staff.GetAsync("/api/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await operational.PostAsJsonAsync($"/api/parking-lots/{siteId}/slots/{slotId}/backup",new{reason="Old token cannot mark"})).StatusCode);
            await Data(await adminClient.PatchAsJsonAsync($"/api/users/{created.GetProperty("id").GetGuid()}/status",new{status="active"}));
            Assert.Equal(HttpStatusCode.Unauthorized,(await staff.GetAsync("/api/auth/me")).StatusCode);await Login(staff,"staff@integration.test");
            await Data(await adminClient.PatchAsJsonAsync($"/api/users/{registration.GetProperty("ownerId").GetGuid()}/status",new{status="locked"}));
            Assert.Equal(HttpStatusCode.Unauthorized,(await lots.GetAsync("/api/parking-lots")).StatusCode);
            using var driver=users.GetTestClient();
            Assert.Equal(HttpStatusCode.BadRequest,(await driver.PostAsJsonAsync("/api/auth/register/driver",new{fullName="Driver",email="driver@integration.test",role="admin"})).StatusCode);
            var driverRegistration=await Data(await driver.PostAsJsonAsync("/api/auth/register/driver",new{fullName="Driver",email="driver@integration.test"}),HttpStatusCode.Created);
            async Task<string> DeliveryCode(string kind)
            {
                await using var scope=users.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var id=(await db.Users.SingleAsync(u=>u.Email=="driver@integration.test")).Id;
                var d=await db.WorkflowDeliveries.Where(d=>d.UserId==id && d.Kind==kind).OrderByDescending(d=>d.CreatedAt).FirstAsync();
                var message=JsonSerializer.Deserialize<WorkflowMessage>(scope.ServiceProvider.GetRequiredService<IWorkflowProtector>().Unprotect(d.ProtectedPayload))!;
                return System.Text.RegularExpressions.Regex.Match(message.Body,@"\d{6}").Value;
            }
            Assert.Equal(HttpStatusCode.OK,(await driver.PostAsJsonAsync("/api/auth/register/driver/verify",new{registrationId=driverRegistration.GetProperty("registrationId").GetGuid(),code=await DeliveryCode("DRIVER_OTP")})).StatusCode);
            var loginChallenge=await Data(await driver.PostAsJsonAsync("/api/auth/otp/request",new{contact="driver@integration.test"}));
            var otpSession=await Data(await driver.PostAsJsonAsync("/api/auth/otp/login",new{challengeId=loginChallenge.GetProperty("challengeId").GetGuid(),code=await DeliveryCode("LOGIN")}));
            driver.DefaultRequestHeaders.Authorization=new("Bearer",otpSession.GetProperty("accessToken").GetString());
            Assert.Equal(86400,otpSession.GetProperty("expiresIn").GetInt32());
            var vehicle=await Data(await driver.PostAsJsonAsync("/api/vehicles",new{plate="51a-123.45",vehicleType="CAR",imageReference="https://images.example.test/car.jpg"}),HttpStatusCode.Created);
            Assert.Equal("51A12345",vehicle.GetProperty("canonicalPlate").GetString());
            Assert.Single((await Data(await driver.GetAsync("/api/vehicles"))).EnumerateArray());
            Assert.Equal(HttpStatusCode.Forbidden,(await adminClient.GetAsync("/api/vehicles")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await owner.GetAsync("/api/vehicles")).StatusCode);
            for(var i=0;i<2;i++)
            {
                await using var db=new NpgsqlConnection(connections[i]);await db.OpenAsync();
                await using var check=new NpgsqlCommand(i==0?"SELECT to_regclass('parking_sites') IS NULL":"SELECT to_regclass('users') IS NULL",db);Assert.True((bool)(await check.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            foreach(var name in names) await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)",admin).ExecuteNonQueryAsync();
        }
    }
}
