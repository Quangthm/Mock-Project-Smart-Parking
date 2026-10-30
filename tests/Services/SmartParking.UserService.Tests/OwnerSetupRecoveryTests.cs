using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Infrastructure.Services;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

public sealed class OwnerSetupRecoveryTests
{
    private static OwnerRegistrationDto Body() => new() { FullName = "Recovery Owner", BusinessName = "Recovery Lot",
        Email = "recovery@example.com", Phone = "0912345678", Password = "Password@123", LotType = "outdoor", AgreedToPolicy = true };
    private static OwnerLotSetupRequest Setup(OwnerApplicationDto application) => new() {
        RegistrationId = application.Id, SetupToken = application.SetupToken!, Lot = new() {
            Name = "Recovered lot", Address = "Test address", LotType = "outdoor", TotalSlots = 445, Floors = 1,
            ModelFileName = "outdoor_parking_lot.glb", PolicyAccepted = true } };
    private static OwnerRegistrationService Service(AppDbContext db, TimeProvider clock) =>
        new(db, new BcryptPasswordService(), clock, new TestParkingDirectory(), WorkflowTestSupport.Create(db, clock));
    private static async Task<string> Code(UserServiceDatabaseFixture fixture, Guid challenge, IWorkflowProtector? protector = null)
    {
        await using var db = fixture.Open();
        var delivery = await db.WorkflowDeliveries.Where(d => d.ChallengeId == challenge && d.Status != "cancelled")
            .OrderByDescending(d => d.CreatedAt).FirstAsync();
        return JsonSerializer.Deserialize<WorkflowMessage>((protector ?? new TestWorkflowProtector()).Unprotect(delivery.ProtectedPayload))!.OtpCode!;
    }

    [PostgresFact]
    public async Task HttpRecoveryRequiresEmailProofRotatesTokenAndCannotVerifyOwnerOrReplay()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        await using var app = await fixture.StartHostAsync(clock);
        using var client = app.GetTestClient();
        var created = await client.PostAsJsonAsync("/api/auth/register/owner", Body());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Deserialize<OwnerApplicationDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var request = await client.PostAsJsonAsync("/api/auth/register/owner/setup-recovery/request", new { contact = Body().Email });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var data = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.False(data.TryGetProperty("setupToken", out _));
        var challenge = data.GetProperty("challengeId").GetGuid();
        var code = await Code(fixture, challenge, app.Services.GetRequiredService<IWorkflowProtector>());
        var wrongPurpose = await client.PostAsJsonAsync("/api/auth/register/owner/verify", new { challengeId = challenge, code });
        Assert.Equal(HttpStatusCode.Conflict, wrongPurpose.StatusCode);
        var completed = await client.PostAsJsonAsync("/api/auth/register/owner/setup-recovery/complete", new { challengeId = challenge, code });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var recovered = (await completed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Deserialize<OwnerApplicationDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.NotEqual(original.SetupToken, recovered.SetupToken);
        Assert.False(recovered.LotSetupComplete);
        Assert.Null(recovered.Verification);
        await using (var db = fixture.Open()) {
            var user = await db.Users.SingleAsync(u => u.Id == recovered.OwnerId);
            Assert.Null(user.EmailVerifiedAt);
            Assert.Equal(UserStatus.PendingVerification, user.Status);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register/owner/setup-recovery/complete", new { challengeId = challenge, code })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/register/owner/lot-setup", Setup(original))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register/owner/lot-setup", Setup(recovered))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register/owner/setup-recovery/request", new { contact = Body().Email })).StatusCode);
    }

    [PostgresFact]
    public async Task RecoveryEnforcesExpiryCooldownAttemptBudgetAcrossResendAndSingleUseConcurrency()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        await using (var db = fixture.Open()) { await Service(db, clock).RegisterAsync(Body(), default); }
        async Task<T> Run<T>(Func<AccountWorkflowService, Task<T>> action) {
            await using var db = fixture.Open(); return await action(WorkflowTestSupport.Create(db, clock));
        }
        async Task Expect(string expected, Func<Task> action) => Assert.Equal(expected, (await Assert.ThrowsAsync<AuthException>(action)).Code);
        var challenge = await Run(s => s.RequestOwnerSetupRecoveryAsync(Body().Email, default));
        var code = await Code(fixture, challenge.ChallengeId);
        string wrong = code == "000000" ? "111111" : "000000";
        Task<OwnerApplicationDto> Complete(string value) => Run(s => s.CompleteOwnerSetupRecoveryAsync(new() { ChallengeId = challenge.ChallengeId, Code = value }, default));
        await Expect("OTP_COOLDOWN", () => Run(s => s.RequestOwnerSetupRecoveryAsync(Body().Email, default)));
        await Expect("OTP_INVALID", () => Complete(wrong));
        clock.Now = clock.Now.AddSeconds(61);
        var resent = await Run(s => s.RequestOwnerSetupRecoveryAsync(Body().Email, default));
        Assert.Equal(challenge.ChallengeId, resent.ChallengeId);
        await Expect("OTP_INVALID", () => Complete(code));
        await using (var db = fixture.Open()) { Assert.Equal(2, (await db.AuthChallenges.SingleAsync(c => c.Id == challenge.ChallengeId)).Attempts); }
        var latest = await Code(fixture, challenge.ChallengeId);
        wrong = latest == "000000" ? "111111" : "000000";
        await Expect("OTP_LOCKED", () => Complete(wrong));
        await Expect("OTP_LOCKED", () => Run(s => s.RequestOwnerSetupRecoveryAsync(Body().Email, default)));
        clock.Now = clock.Now.AddMinutes(16);
        await Run(s => s.RequestOwnerSetupRecoveryAsync(Body().Email, default));
        latest = await Code(fixture, challenge.ChallengeId);
        clock.Now = clock.Now.AddMinutes(6);
        await Expect("OTP_EXPIRED", () => Complete(latest));
        await Run(s => s.RequestOwnerSetupRecoveryAsync(Body().Email, default));
        latest = await Code(fixture, challenge.ChallengeId);
        async Task<bool> Race() { try { await Complete(latest); return true; } catch (AuthException e) { Assert.Equal("CHALLENGE_CLOSED", e.Code); return false; } }
        Assert.Equal(1, (await Task.WhenAll(Race(), Race())).Count(value => value));
    }

    [PostgresFact]
    public async Task LegacyVerifiedIncompleteOwnerRecoversThenSavesAndRetriesWithoutDuplicateContactChallenge()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var clock = new TestClock();
        OwnerApplicationDto original;
        await using (var db = fixture.Open()) {
            original = await Service(db, clock).RegisterAsync(Body(), default);
            var application = await db.OwnerApplications.SingleAsync(a => a.Id == original.Id);
            application.SetupTokenHash = null;
            application.SetupTokenExpiresAt = null;
            var user = await db.Users.SingleAsync(u => u.Id == original.OwnerId);
            user.EmailVerifiedAt = clock.Now.AddDays(-1);
            user.Status = UserStatus.PendingApproval;
            db.AuthChallenges.Add(new AuthChallenge { Id = Guid.NewGuid(), UserId = original.OwnerId, Purpose = "OWNER_CONTACT", Channel = "email",
                Destination = Body().Email, CodeHash = "", ExpiresAt = clock.Now.AddDays(-1), ResendAt = clock.Now.AddDays(-1), ConsumedAt = clock.Now.AddDays(-1) });
            await db.SaveChangesAsync();
        }
        ChallengeResult challenge;
        await using (var db = fixture.Open()) { challenge = await WorkflowTestSupport.Create(db, clock).RequestOwnerSetupRecoveryAsync(Body().Email, default); }
        var code = await Code(fixture, challenge.ChallengeId);
        OwnerApplicationDto recovered;
        await using (var db = fixture.Open()) { recovered = await WorkflowTestSupport.Create(db, clock).CompleteOwnerSetupRecoveryAsync(new() { ChallengeId = challenge.ChallengeId, Code = code }, default); }
        Assert.False(recovered.ContactVerified);
        OwnerApplicationDto saved;
        await using (var db = fixture.Open()) { saved = await Service(db, clock).SaveLotSetupAsync(Setup(recovered), default); }
        Assert.False(saved.ContactVerified);
        await using (var db = fixture.Open()) {
            var retry = await Service(db, clock).SaveLotSetupAsync(Setup(recovered), default);
            Assert.Equal(saved.Verification, retry.Verification);
            var status = await WorkflowTestSupport.Create(db, clock).RecoverOwnerAsync(Body().Email, default);
            var statusJson = JsonSerializer.SerializeToElement(status);
            Assert.True(statusJson.GetProperty("lotSetupComplete").GetBoolean());
            // OwnerVerification's mount check must not auto-skip the new OTP screen.
            Assert.False(statusJson.GetProperty("emailVerified").GetBoolean());
            Assert.Equal(1, await db.AuthChallenges.CountAsync(c => c.UserId == original.OwnerId && c.Purpose == "OWNER_CONTACT"));
        }
        var contactCode = await Code(fixture, saved.Verification!.ChallengeId);
        await using (var db = fixture.Open()) {
            await WorkflowTestSupport.Create(db, clock).VerifyOwnerAsync(saved.Verification.ChallengeId, contactCode, default);
            Assert.Equal(UserStatus.PendingApproval, (await db.Users.SingleAsync(u => u.Id == original.OwnerId)).Status);
        }
    }
}
