using System.Security.Cryptography;
using System.Text;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

namespace UserService.Application.Services;

public sealed partial class AccountWorkflowService
{
    private const string SetupRecoveryPurpose = "OWNER_SETUP_RECOVERY";

    private async Task<User> RequireRecoverableOwnerAsync(OwnerApplication application, CancellationToken ct)
    {
        var user = await db.LockUserAsync(application.UserId, ct);
        if (application.Status != "pending" || application.ApprovalRequestedBy is not null ||
            application.LotSetupJson is not null || user.DeletedOn is not null ||
            user.Status is not (UserStatus.PendingVerification or UserStatus.PendingApproval) ||
            !await db.HasPendingOwnerAccountAsync(application.UserId, ct))
            throw Error("APPLICATION_CLOSED", "Setup recovery is unavailable. Check your registration status.", 409);
        application.User = user;
        return user;
    }

    public async Task<ChallengeResult> RequestOwnerSetupRecoveryAsync(string contact, CancellationToken ct)
    {
        contact = Contact(contact);
        if (!contact.Contains('@')) throw Error("VALIDATION_FAILED", "Use your registered email address.");
        await using var tx = await db.BeginTransactionAsync(ct);
        var found = await db.FindOwnerApplicationByEmailAsync(contact, ct)
            ?? throw Error("REGISTRATION_NOT_FOUND", "Pending Business registration was not found.", 404);
        // Match setup saving and approval: application -> user -> challenge.
        var application = await db.LockOwnerApplicationAsync(found.Id, ct)
            ?? throw Error("REGISTRATION_NOT_FOUND", "Pending Business registration was not found.", 404);
        var user = await RequireRecoverableOwnerAsync(application, ct);
        var challenge = await db.FindOwnerSetupRecoveryChallengeAsync(application.UserId, ct);
        var now = clock.GetUtcNow();
        if (challenge is null)
            challenge = await IssueAsync(user, SetupRecoveryPurpose, "email", user.Email!, ct);
        else
        {
            if (challenge.LockedUntil > now) throw Error("OTP_LOCKED", "Try again after the OTP lock expires.", 423);
            if (challenge.ResendAt > now) throw Error("OTP_COOLDOWN", "Wait 60 seconds before requesting another code.", 429);
            if (challenge.LockedUntil is not null) { challenge.LockedUntil = null; challenge.Attempts = 0; }
            var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
            while (!string.IsNullOrEmpty(challenge.CodeHash) && passwords.Verify(code, challenge.CodeHash))
                code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
            challenge.CodeHash = passwords.Hash(code);
            challenge.ConsumedAt = null;
            challenge.ExpiresAt = now.AddMinutes(5);
            challenge.ResendAt = now.AddMinutes(1);
            await QueueAsync(user.Id!.Value, user.Id.Value, SetupRecoveryPurpose,
                OtpWorkflowMessage.Create("email", user.Email!, code), ct, challenge.Id);
        }
        await tx.CommitAsync(ct);
        return Result(challenge);
    }

    public async Task<OwnerApplicationDto> CompleteOwnerSetupRecoveryAsync(CompleteOtpLoginDto body, CancellationToken ct)
    {
        Validate(body);
        await using var tx = await db.BeginTransactionAsync(ct);
        var userId = await db.FindChallengeUserIdAsync(body.ChallengeId, ct)
            ?? throw Error("CHALLENGE_CLOSED", "Recovery challenge is unavailable.", 409);
        var found = await db.FindOwnerApplicationByUserAsync(userId, ct)
            ?? throw Error("CHALLENGE_CLOSED", "Recovery challenge is unavailable.", 409);
        var application = await db.LockOwnerApplicationAsync(found.Id, ct)
            ?? throw Error("CHALLENGE_CLOSED", "Recovery challenge is unavailable.", 409);
        var user = await RequireRecoverableOwnerAsync(application, ct);
        var challenge = await db.LockChallengeAsync(body.ChallengeId, ct);
        if (challenge is null || challenge.UserId != userId || challenge.Purpose != SetupRecoveryPurpose ||
            challenge.ConsumedAt is not null || challenge.Channel != "email" || challenge.Destination != user.Email)
            throw Error("CHALLENGE_CLOSED", "Recovery challenge is unavailable.", 409);
        if (challenge.LockedUntil > clock.GetUtcNow()) throw Error("OTP_LOCKED", "Try again after the OTP lock expires.", 423);
        if (challenge.LockedUntil is not null) { challenge.LockedUntil = null; challenge.Attempts = 0; }
        if (!await CheckAsync(challenge, body.Code, ct))
        {
            await tx.CommitAsync(ct);
            throw Error(challenge.LockedUntil is null ? "OTP_INVALID" : "OTP_LOCKED", "Invalid recovery code.", challenge.LockedUntil is null ? 400 : 423);
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        application.SetupTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        application.SetupTokenExpiresAt = clock.GetUtcNow().AddHours(24);
        challenge.ConsumedAt = clock.GetUtcNow();
        challenge.CodeHash = "";
        challenge.Attempts = 0;
        // Setup recovery proves access only. The owner workflow verifies contact again after the lot is saved.
        user.EmailVerifiedAt = null;
        user.Status = UserStatus.PendingVerification;
        user.ModifiedOn = clock.GetUtcNow();
        await db.CancelChallengeDeliveriesAsync(challenge.Id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(application.Id, user.Id!.Value, user.FullName, application.BusinessName, user.Email!, user.Phone!,
            application.LotType, application.Status, application.SubmittedAt, application.ReviewedAt,
            application.ReviewedBy, application.ReviewNote, ContactVerified: user.EmailVerifiedAt is not null,
            LotSetupComplete: false, SetupToken: token);
    }
}
