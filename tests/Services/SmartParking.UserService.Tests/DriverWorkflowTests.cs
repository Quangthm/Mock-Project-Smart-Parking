using UserService.Application.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.UserService.Domain.Enum;
using UserService.API.Services;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class DriverWorkflowTests
{
    [Theory]
    [InlineData("0901234567", true)]
    [InlineData(" +84901234567 ", true)]
    [InlineData("84901234567", true)]
    [InlineData("090 1234567", false)]
    [InlineData("driver@example.test", false)]
    [InlineData("+12345678", false)]
    [InlineData("1234567890123456", false)]
    public void RegistrationPhoneUsesTheSameTrimmedContactRules(string phone, bool valid)
    {
        var body = new DriverRegistrationDto { FullName = "Driver", Phone = phone };
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.Equal(valid, System.ComponentModel.DataAnnotations.Validator.TryValidateObject(body, new(body), errors, true));
    }

    [PostgresFact]
    public async Task ConcurrentWrongCodesLockForExactlyFifteenMinutesAndResendCannotResetAttempts()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock(); DriverRegistrationResult registration; string original;
        await using (var db = fixture.Open())
        {
            registration = await Registration(db, clock).RegisterAsync(new() { FullName = "Driver", Email = "lock@example.test" }, default);
            original = await CodeAsync(db, (await db.Users.SingleAsync()).Id!.Value);
        }
        var wrong = original == "000000" ? "000001" : "000000";
        async Task<string> VerifyWrong()
        {
            await using var db = fixture.Open();
            return (await Assert.ThrowsAsync<AuthException>(() => Registration(db, clock).VerifyAsync(new() { RegistrationId = registration.RegistrationId, Code = wrong }, default))).Code;
        }
        Assert.Equal("OTP_INVALID", await VerifyWrong());
        clock.Now = registration.ResendAvailableAt;
        await using (var db = fixture.Open())
        {
            await Registration(db, clock).ResendAsync(registration.RegistrationId, default);
            Assert.Equal(1, (await db.DriverRegistrations.SingleAsync()).FailedAttempts);
            var resent = await CodeAsync(db, (await db.Users.SingleAsync()).Id!.Value);
            wrong = resent == "000000" ? "000001" : "000000";
        }
        var outcomes = await Task.WhenAll(VerifyWrong(), VerifyWrong());
        Assert.Single(outcomes, c => c == "OTP_INVALID"); Assert.Single(outcomes, c => c == "OTP_LOCKED");
        var lockDeadline = clock.Now.AddMinutes(15);
        clock.Now = lockDeadline.AddTicks(-10);
        await using (var db = fixture.Open())
        {
            Assert.Equal("OTP_LOCKED", (await Assert.ThrowsAsync<AuthException>(() => Registration(db, clock).ResendAsync(registration.RegistrationId, default))).Code);
            Assert.Equal(lockDeadline, (await db.DriverRegistrations.SingleAsync()).LockedUntil);
        }
        clock.Now = lockDeadline;
        await using (var db = fixture.Open())
        {
            await Registration(db, clock).ResendAsync(registration.RegistrationId, default);
            var challenge = await db.DriverRegistrations.SingleAsync();
            Assert.Null(challenge.LockedUntil); Assert.Equal(0, challenge.FailedAttempts);
            var code = await CodeAsync(db, (await db.Users.SingleAsync()).Id!.Value);
            await Registration(db, clock).VerifyAsync(new() { RegistrationId = registration.RegistrationId, Code = code }, default);
            Assert.Equal(UserStatus.Active, (await db.Users.SingleAsync()).Status);
            Assert.Single(await db.Accounts.ToArrayAsync());
        }
    }
    [PostgresFact]
    public async Task InvalidRegistrationLeavesNoUserAccountChallengeOrDelivery()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        foreach (var body in new DriverRegistrationDto[]
        {
            new() { FullName = "Driver" }, new() { FullName = " ", Email = "valid@example.test" },
            new() { FullName = "Driver", Email = "bad-email" },
            new() { FullName = "Driver", Phone = "driver@example.test" },
            new() { FullName = "Driver", Phone = "090 1234567" },
            new() { FullName = "Driver", Email = "valid@example.test", Password = "weak" }
        })
        {
            await using var db = fixture.Open();
            Assert.Equal("VALIDATION_FAILED", (await Assert.ThrowsAsync<AuthException>(() => Registration(db, new TestClock()).RegisterAsync(body, default))).Code);
        }
        await using var check = fixture.Open();
        Assert.Empty(await check.Users.ToArrayAsync()); Assert.Empty(await check.Accounts.ToArrayAsync());
        Assert.Empty(await check.DriverRegistrations.ToArrayAsync()); Assert.Empty(await check.WorkflowDeliveries.ToArrayAsync());
    }
    private sealed class Transport : IWorkflowTransport
    {
        public bool Fail { get; set; }
        public List<Guid> Sent { get; } = [];
        public Task SendAsync(WorkflowMessage message, Guid id, CancellationToken ct)
        {
            if (Fail) throw new IOException("Simulated provider outage.");
            Sent.Add(id); return Task.CompletedTask;
        }
    }
    private static DriverRegistrationService Registration(AppDbContext db, TestClock clock)
    {
        var workflows = WorkflowTestSupport.Create(db, clock);
        return new(db, new BcryptPasswordService(), new QueuedOtpSender(db, workflows), clock);
    }
    private static async Task<string> CodeAsync(AppDbContext db, Guid userId)
    {
        var delivery = await db.WorkflowDeliveries.AsNoTracking().SingleAsync(d => d.UserId == userId && d.Kind == "DRIVER_OTP" && d.Status == "pending");
        var message = JsonSerializer.Deserialize<WorkflowMessage>(new TestWorkflowProtector().Unprotect(delivery.ProtectedPayload))!;
        return Regex.Match(message.Body, @"\d{6}").Value;
    }

    [PostgresFact]
    public async Task PasswordlessNormalizedContactsConcurrentVerificationAndNoEarlySession()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        Guid userId; DriverRegistrationResult registration; string code;
        await using (var db = fixture.Open())
        {
            var service = Registration(db, clock);
            registration = await service.RegisterAsync(new() { FullName = " Driver ", Email = " DRIVER@EXAMPLE.TEST ", Phone = " +84911223344 " }, default);
            var user = await db.Users.SingleAsync(); userId = user.Id!.Value;
            Assert.Equal("driver@example.test", user.Email); Assert.Equal("0911223344", user.Phone);
            Assert.Equal("", user.PasswordHash); Assert.Equal("email", registration.Channel);
            Assert.Equal(UserStatus.PendingVerification, user.Status);
            Assert.Equal("DRIVER", (await db.AccountRoles.SingleAsync()).RoleCode);
            Assert.Empty(await db.AuthSessions.ToArrayAsync());
            Assert.Equal(registration.RegistrationId, (await service.RecoverAsync("DRIVER@EXAMPLE.TEST", default)).RegistrationId);
            code = await CodeAsync(db, userId);
            Assert.Equal("ACCOUNT_PENDING_VERIFICATION", (await Assert.ThrowsAsync<AuthException>(() => WorkflowTestSupport.Create(db, clock).RequestLoginAsync(user.Email!, default))).Code);
        }
        async Task<string> Verify()
        {
            await using var db = fixture.Open();
            try { await Registration(db, clock).VerifyAsync(new() { RegistrationId = registration.RegistrationId, Code = code }, default); return "verified"; }
            catch (AuthException e) { return e.Code; }
        }
        var outcomes = await Task.WhenAll(Verify(), Verify());
        Assert.Single(outcomes, s => s == "verified"); Assert.Single(outcomes, s => s == "REGISTRATION_CLOSED");
        await using (var db = fixture.Open())
        {
            var user = await db.Users.SingleAsync();
            Assert.Equal(UserStatus.Active, user.Status); Assert.NotNull(user.EmailVerifiedAt); Assert.Null(user.PhoneVerifiedAt);
            Assert.Single(await db.Users.ToArrayAsync()); Assert.Single(await db.Accounts.ToArrayAsync());
            Assert.Empty(await db.AuthSessions.ToArrayAsync());
            var workflows = WorkflowTestSupport.Create(db, clock);
            Assert.Equal("CONTACT_UNVERIFIED", (await Assert.ThrowsAsync<AuthException>(() => workflows.RequestLoginAsync("+84911223344", default))).Code);
            Assert.Equal("CONTACT_EXISTS", (await Assert.ThrowsAsync<AuthException>(() => Registration(db, clock).RegisterAsync(new() { FullName = "Duplicate", Phone = "84911223344" }, default))).Code);
        }
    }

    [PostgresFact]
    public async Task ExactExpiryResendCancellationAndRecoverableProviderFailure()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock(); DriverRegistrationResult registration; Guid userId; string original;
        await using (var db = fixture.Open())
        {
            registration = await Registration(db, clock).RegisterAsync(new() { FullName = "Phone Driver", Phone = "0901234567" }, default);
            userId = (await db.Users.SingleAsync()).Id!.Value; original = await CodeAsync(db, userId);
        }
        async Task<string> Verify(string code)
        {
            await using var db = fixture.Open();
            return (await Assert.ThrowsAsync<AuthException>(() => Registration(db, clock).VerifyAsync(new() { RegistrationId = registration.RegistrationId, Code = code }, default))).Code;
        }
        clock.Now = registration.ExpiresAt;
        Assert.Equal("OTP_EXPIRED", await Verify(original));
        clock.Now = clock.Now.AddTicks(1);
        Assert.Equal("OTP_EXPIRED", await Verify(original));
        await using (var db = fixture.Open())
        {
            await Registration(db, clock).ResendAsync(registration.RegistrationId, default);
            var cancelled = await db.WorkflowDeliveries.SingleAsync(d => d.Status == "cancelled");
            Assert.Equal("", cancelled.ProtectedPayload);
            Assert.Single(await db.WorkflowDeliveries.Where(d => d.Status == "pending").ToArrayAsync());
        }
        Assert.Equal("OTP_INVALID", await Verify(original));
        Guid deliveryId;
        var transport = new Transport { Fail = true };
        await using (var db = fixture.Open())
        {
            await WorkflowTestSupport.Create(db, clock).DeliverOneAsync(transport, default);
            var delivery = await db.WorkflowDeliveries.SingleAsync(d => d.Status == "failed");
            deliveryId = delivery.Id;
            Assert.Equal(1, delivery.Attempts); Assert.NotEqual("", delivery.ProtectedPayload);
            Assert.Equal(UserStatus.PendingVerification, (await db.Users.SingleAsync()).Status);
            Assert.Single(await db.Users.ToArrayAsync()); Assert.Single(await db.Accounts.ToArrayAsync());
        }
        transport.Fail = false; clock.Now = clock.Now.AddMinutes(2);
        await using (var db = fixture.Open())
        {
            await WorkflowTestSupport.Create(db, clock).DeliverOneAsync(transport, default);
            var delivery = await db.WorkflowDeliveries.SingleAsync(d => d.Id == deliveryId);
            Assert.Equal("sent", delivery.Status); Assert.Equal(2, delivery.Attempts); Assert.Equal("", delivery.ProtectedPayload);
            Assert.Equal(deliveryId, Assert.Single(transport.Sent));
            Assert.Single(await db.Users.ToArrayAsync()); Assert.Single(await db.Accounts.ToArrayAsync());
            var user = await db.Users.SingleAsync();
            // OTP remains valid just before the exact deadline; verifying never issues a session.
            var challenge = await db.DriverRegistrations.SingleAsync();
            clock.Now = challenge.ExpiresAt.AddTicks(-10);
            // Read the retained hash through a fresh resend rather than decrypting a sent payload.
            await Registration(db, clock).ResendAsync(registration.RegistrationId, default);
            var code = await CodeAsync(db, userId);
            clock.Now = (await db.DriverRegistrations.SingleAsync()).ExpiresAt.AddTicks(-10);
            await Registration(db, clock).VerifyAsync(new() { RegistrationId = registration.RegistrationId, Code = code }, default);
            Assert.Equal(UserStatus.Active, user.Status); Assert.NotNull(user.PhoneVerifiedAt);
            Assert.Empty(await db.AuthSessions.ToArrayAsync());
        }
    }

    [PostgresFact]
    public async Task RealHttpPrivilegeInjectionPasswordlessVerificationAndOtpLogin()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        await using var app = await fixture.StartHostAsync();
        using var client = app.GetTestClient();
        foreach (var field in new[] { "role", "status", "permissions" })
        {
            var payload = new Dictionary<string, object> { ["fullName"] = "Driver", ["email"] = "http-driver@example.test", [field] = "ADMIN" };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register/driver", payload)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register/driver", new { fullName = "Driver" })).StatusCode);
        await using (var db = fixture.Open()) { Assert.Empty(await db.Users.ToArrayAsync()); Assert.Empty(await db.Accounts.ToArrayAsync()); }
        var created = await client.PostAsJsonAsync("/api/auth/register/driver", new { fullName = "Driver", email = "http-driver@example.test", phone = " +84912345678 " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.False(data.TryGetProperty("code", out _)); Assert.False(data.TryGetProperty("accessToken", out _));
        var id = data.GetProperty("registrationId").GetGuid(); string code;
        // Test-only access to the encrypted outbox; the public API cannot read OTPs.
        await using (var db = fixture.Open())
        {
            Assert.Equal("0912345678", (await db.Users.SingleAsync()).Phone);
            var delivery = await db.WorkflowDeliveries.SingleAsync();
            using var scope = app.Services.CreateScope();
            var protector = scope.ServiceProvider.GetRequiredService<IWorkflowProtector>();
            code = Regex.Match(JsonSerializer.Deserialize<WorkflowMessage>(protector.Unprotect(delivery.ProtectedPayload))!.Body, @"\d{6}").Value;
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register/driver/verify", new { registrationId = id, code })).StatusCode);
        var requested = await client.PostAsJsonAsync("/api/auth/otp/request", new { contact = "http-driver@example.test" });
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var challengeId = (await requested.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("challengeId").GetGuid();
        await using (var db = fixture.Open())
        {
            Assert.Empty(await db.AuthSessions.ToArrayAsync());
            var delivery = await db.WorkflowDeliveries.SingleAsync(d => d.ChallengeId == challengeId);
            using var scope = app.Services.CreateScope();
            code = Regex.Match(JsonSerializer.Deserialize<WorkflowMessage>(scope.ServiceProvider.GetRequiredService<IWorkflowProtector>().Unprotect(delivery.ProtectedPayload))!.Body, @"\d{6}").Value;
        }
        var loggedIn = await client.PostAsJsonAsync("/api/auth/otp/login", new { challengeId, code });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        var session = (await loggedIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/vehicles")).StatusCode);
        await using (var db = fixture.Open())
        {
            await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/vehicles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/otp/request", new { contact = "http-driver@example.test" })).StatusCode);
    }
}
