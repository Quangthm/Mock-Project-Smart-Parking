using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Usecase.Session;
using UserService.Infrastructure.Services;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class IdentityAccessHardeningTests
{
    [Theory]
    [InlineData(86399, true)]
    [InlineData(86400, false)]
    [InlineData(86401, false)]
    public async Task RefreshInactivityHasStrictBoundary(int elapsedSeconds, bool accepted)
    {
        var f = new AuthTestFixture();
        var session = (await f.SignIn()).Session!;
        var issued = f.Clock.Now;
        f.Clock.Now = issued.AddHours(23);
        await f.Sessions.IsActiveAsync(f.Sessions.Snapshot.Single().AccessTokenId, f.User.Id!.Value, default);
        Assert.Equal(issued, f.Sessions.Snapshot.Single().LastRefreshedAt);
        f.Clock.Now = issued.AddSeconds(elapsedSeconds);
        var handler = new RefreshCommandHandler(f, f.Sessions, f.Auth, f.Clock);
        if (accepted)
        {
            await handler.Handle(new(session.RefreshToken), default);
            Assert.Equal(f.Clock.Now, f.Sessions.Snapshot.Single().LastRefreshedAt);
            Assert.Equal(issued.AddDays(7), f.Sessions.Snapshot.Single().ExpiresAt);
        }
        else Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => handler.Handle(new(session.RefreshToken), default))).Code);
    }

    [Fact]
    public async Task SuccessfulRefreshNeverExtendsAbsoluteDeadlineAndCapsFinalAccess()
    {
        var f = new AuthTestFixture();
        var issued = f.Clock.Now;
        var session = (await f.SignIn()).Session!;
        var handler = new RefreshCommandHandler(f, f.Sessions, f.Auth, f.Clock);
        for (var hour = 23; hour <= 161; hour += 23)
        {
            f.Clock.Now = issued.AddHours(hour);
            session = await handler.Handle(new(session.RefreshToken), default);
        }
        f.Clock.Now = issued.AddDays(7).AddMinutes(-1);
        session = await handler.Handle(new(session.RefreshToken), default);
        Assert.Equal(60, session.ExpiresIn);
        Assert.Equal(issued.AddDays(7), f.Sessions.Snapshot.Single().ExpiresAt);
        f.Clock.Now = issued.AddDays(7);
        await Assert.ThrowsAsync<AuthException>(() => handler.Handle(new(session.RefreshToken), default));
    }

    [PostgresFact]
    public async Task ScopedOwnerGrantsAndOperatorActivationThroughProductionHttpPipeline()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        var owner = NewUser("owner-hardening@example.com", "BUSINESS_OWNER", clock);
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        await fixture.SeedTenantsAsync(tenantA, tenantB);
        owner.Accounts.Single().TenantId = tenantA;
        owner.Accounts.Single().Permissions = ["OPERATOR_MANAGE", "PARKING_MANAGE"];
        var denied = new Account { Id = Guid.NewGuid(), User = owner, UserId = owner.Id!.Value,
            TenantId = tenantB, Status = "ACTIVE", Permissions = [] };
        denied.AccountRoles.Add(new() { AccountId = denied.Id.Value, Account = denied, RoleCode = "BUSINESS_OWNER" });
        owner.Accounts.Add(denied);
        await using (var db = fixture.Open()) { db.Users.Add(owner); await db.SaveChangesAsync(); }
        var directory = new TestParkingDirectory();
        var siteA = Guid.NewGuid(); var siteB = Guid.NewGuid();
        directory.Sites[siteA] = tenantA; directory.Sites[siteB] = tenantB;
        await using var app = await fixture.StartHostAsync(clock, builder =>
            builder.Services.AddSingleton<IParkingDirectory>(directory));
        using var client = app.GetTestClient();
        var ownerSession = await Login(client, owner.Email!, "Password@123");
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerSession.AccessToken);
        var scope = await client.GetFromJsonAsync<JsonElement>("/api/auth/owner-scope?permission=PARKING_MANAGE");
        Assert.Equal(new[] { tenantA }, scope.GetProperty("tenantIds").EnumerateArray().Select(x => x.GetGuid()).ToArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/owner-scope?permission=BACKUP_MARK")).StatusCode);
        CreateOperatorDto Body(Guid site) => new() { FullName = "Invited Operator", Email = "operator-hardening@example.com",
            SiteIds = [site], Permissions = ["SLOT_OVERRIDE"] };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", Body(siteB))).StatusCode);
        var created = await client.PostAsJsonAsync("/api/users", Body(siteA));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("pending_verification", result.GetProperty("status").GetString());
        var operatorId = result.GetProperty("id").GetGuid();
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = Body(siteA).Email, password = "Password@123" })).StatusCode);
        ChangeBootstrapPasswordDto activation;
        await using (var db = fixture.Open())
        {
            Assert.Empty(await new IdentityAccessRepository(db).AssignmentsAsync(operatorId, default));
            Assert.Empty((await db.Users.SingleAsync(user => user.Id == operatorId)).PasswordHash);
            var delivery = await db.WorkflowDeliveries.SingleAsync(d => d.UserId == operatorId);
            using var services = app.Services.CreateScope();
            var message = JsonSerializer.Deserialize<WorkflowMessage>(services.ServiceProvider.GetRequiredService<IWorkflowProtector>().Unprotect(delivery.ProtectedPayload))!;
            Assert.DoesNotContain("Password@123", message.Body);
            activation = new() { ChallengeId = delivery.ChallengeId!.Value,
                Token = Regex.Match(message.Body, @"token=([A-F0-9]{64})").Groups[1].Value, Password = "Operator@123" };
        }
        var invalid = new ChangeBootstrapPasswordDto { ChallengeId = activation.ChallengeId, Token = new string('A',64), Password = activation.Password };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/bootstrap-password", invalid)).StatusCode);
        // Expired and historical consumed invitations can be replaced; the old link stays unusable.
        var oldActivation = activation;
        clock.Now = clock.Now.AddHours(24);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/bootstrap-password", oldActivation)).StatusCode);
        ownerSession = await Login(client, owner.Email!, "Password@123");
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/operators/{operatorId}/invitation", null)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync($"/api/operators/{operatorId}/invitation", null)).StatusCode);
        await using (var db=fixture.Open())
        {
            var delivery=await db.WorkflowDeliveries.Where(d=>d.UserId==operatorId).OrderByDescending(d=>d.CreatedAt).FirstAsync();
            using var services=app.Services.CreateScope();
            var message=JsonSerializer.Deserialize<WorkflowMessage>(services.ServiceProvider.GetRequiredService<IWorkflowProtector>().Unprotect(delivery.ProtectedPayload))!;
            activation=new(){ChallengeId=delivery.ChallengeId!.Value,Token=Regex.Match(message.Body,@"token=([A-F0-9]{64})").Groups[1].Value,Password="Operator@123"};
        }
        client.DefaultRequestHeaders.Authorization=null;
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/bootstrap-password",oldActivation)).StatusCode);
        var outcomes = await Task.WhenAll(Enumerable.Range(0,2).Select(_ => client.PostAsJsonAsync("/api/auth/bootstrap-password", activation)));
        Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.Unauthorized);
        var session = await Login(client, Body(siteA).Email, activation.Password);
        Assert.Equal(900, session.ExpiresIn);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        var assignments = (await client.GetFromJsonAsync<JsonElement>("/api/operators/me")).GetProperty("data");
        Assert.Equal(siteA, Assert.Single(assignments.EnumerateArray()).GetProperty("siteId").GetGuid());
        await using (var db = fixture.Open())
        {
            var user = await db.Users.SingleAsync(u => u.Id == operatorId);
            Assert.NotNull(user.EmailVerifiedAt);
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.Equal(2, await db.AccessEventOutbox.CountAsync());
            // Explicit empty grants revoke only A; no action may borrow permissions from another membership.
            await db.Accounts.Where(a => a.UserId == owner.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Permissions, Array.Empty<string>()));
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerSession.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/operators")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/owner-scope")).StatusCode);
    }

    [PostgresFact]
    public async Task OutboxRollbackLeaseRecoveryInboxDeduplicationAndVersionGuard()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        var message = new OperatorAccessChanged(Guid.NewGuid(),1,Guid.NewGuid(),2,clock.Now,Guid.NewGuid(),Guid.NewGuid(),[Guid.NewGuid()],"active");
        await using (var db = fixture.Open())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            new AccessEventStore(db,clock).Stage(message);
            await db.SaveChangesAsync();
            await tx.RollbackAsync();
        }
        await using (var db = fixture.Open())
        {
            Assert.Empty(await db.AccessEventOutbox.ToArrayAsync());
            new AccessEventStore(db,clock).Stage(message);
            await db.SaveChangesAsync();
        }
        async Task<AccessEventLease?> Claim()
        { await using var db=fixture.Open(); return await new AccessEventStore(db,clock).ClaimAsync(default); }
        var claims = await Task.WhenAll(Claim(),Claim());
        var first = Assert.Single(claims.OfType<AccessEventLease>());
        clock.Now = clock.Now.AddSeconds(31);
        var retried = (await Claim())!;
        Assert.Equal(first.Message.EventId,retried.Message.EventId);
        Assert.NotEqual(first.LeaseId,retried.LeaseId);
        await using (var db=fixture.Open())
        {
            var events = new AccessEventStore(db,clock);
            await events.CompleteAsync(first,default);
            Assert.Null((await db.AccessEventOutbox.AsNoTracking().SingleAsync()).PublishedAt);
            await events.CompleteAsync(retried,default);
            Assert.NotNull((await db.AccessEventOutbox.AsNoTracking().SingleAsync()).PublishedAt);
            Assert.True(await events.ApplyProjectionAsync(message,default));
            Assert.False(await events.ApplyProjectionAsync(message,default));
            Assert.False(await events.ApplyProjectionAsync(message with { EventId=Guid.NewGuid(), AggregateVersion=1, Status="pending_verification" },default));
        }
    }

    [PostgresFact]
    public async Task RefreshDatabaseCasEnforcesIdleBoundaryAndUpdatesOnlySuccessfulRefresh()
    {
        await using var fixture=await UserServiceDatabaseFixture.CreateAsync();
        var clock=new TestClock();
        var user=NewUser("refresh-hardening@example.com","DRIVER",clock);
        var session=new AuthSession {Id=Guid.NewGuid(),UserId=user.Id!.Value,AccessTokenId=Guid.NewGuid(),TokenHash="initial",
            CreatedAt=clock.Now,LastRefreshedAt=clock.Now,AccessExpiresAt=clock.Now.AddMinutes(15),ExpiresAt=clock.Now.AddDays(7)};
        await using(var db=fixture.Open()){db.Users.Add(user);db.AuthSessions.Add(session);await db.SaveChangesAsync();}
        var issued=clock.Now;
        clock.Now=issued.AddHours(23);
        async Task<bool> Rotate(AuthSession previous)
        { await using var db=fixture.Open(); return await new PostgresAuthSessionStore(db,clock).RotateAsync(previous,Guid.NewGuid(),Guid.NewGuid().ToString(),clock.Now.AddMinutes(15),default); }
        Assert.Single(await Task.WhenAll(Rotate(session),Rotate(session)), x=>x);
        AuthSession current;
        await using(var db=fixture.Open())
        {
            current=await db.AuthSessions.AsNoTracking().SingleAsync();
            Assert.Equal(clock.Now,current.LastRefreshedAt);
            Assert.Equal(session.ExpiresAt,current.ExpiresAt);
            await new PostgresAuthSessionStore(db,clock).IsActiveAsync(current.AccessTokenId,user.Id.Value,default);
        }
        clock.Now=clock.Now.AddHours(24);
        Assert.False(await Rotate(current));
        await using(var db=fixture.Open()) Assert.Equal(issued.AddHours(23),(await db.AuthSessions.SingleAsync()).LastRefreshedAt);
    }

    private static User NewUser(string email,string role,TestClock clock)
    {
        var user=new User {Id=Guid.NewGuid(),Email=email,EmailVerifiedAt=clock.Now,FullName="Test User",Status=UserStatus.Active,
            PasswordHash=new BcryptPasswordService().Hash("Password@123"),CreatedOn=clock.Now,ModifiedOn=clock.Now};
        var account=new Account {Id=Guid.NewGuid(),User=user,UserId=user.Id.Value,Status="ACTIVE"};
        account.AccountRoles.Add(new(){AccountId=account.Id.Value,Account=account,RoleCode=role});
        user.Accounts.Add(account);return user;
    }
    private static async Task<UserSessionDto> Login(HttpClient client,string email,string password)
    {
        var response=await client.PostAsJsonAsync("/api/auth/login",new{email,password});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        return data.Deserialize<UserSessionDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
