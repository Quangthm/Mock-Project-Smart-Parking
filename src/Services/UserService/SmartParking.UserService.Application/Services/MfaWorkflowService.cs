using UserService.Application.Common.Interfaces.Persistence;
using System.Security.Cryptography;
using System.Text;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Models.JwT;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.Application.Services;

// MFA shares the account workflow's DbContext and transaction. Lock the user first,
// then the credential, so login and security changes serialize across API instances.
public sealed partial class AccountWorkflowService
{
    private const int RecoveryCodeCount = 8;

    public Task<bool> MfaEnabledAsync(Guid id, CancellationToken ct) =>
        db.IsMfaEnabledAsync(id, ct);

    private Task<int> RemainingRecoveryCodesAsync(Guid id, CancellationToken ct) =>
        db.CountUnusedRecoveryCodesAsync(id, ct);

    public async Task<MfaStatusDto> MfaStatusAsync(Guid id, CancellationToken ct)
    {
        var enabled = await MfaEnabledAsync(id, ct);
        return new(enabled, enabled ? await RemainingRecoveryCodesAsync(id, ct) : 0);
    }

    // Called inside LoginCommandHandler's transaction, after password verification.
    public async Task<MfaLoginChallengeDto> CreateMfaLoginChallengeAsync(Guid id, CancellationToken ct)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var challenge = new AuthChallenge
        {
            Id = Guid.NewGuid(),
            UserId = id,
            Purpose = "PASSWORD_MFA",
            CodeHash = AuthSessionService.HashRefreshToken(token),
            ExpiresAt = clock.GetUtcNow().AddMinutes(5),
            ResendAt = clock.GetUtcNow()
        };
        db.Stage(challenge);
        await db.SaveChangesAsync(ct);
        return new(challenge.Id, token, challenge.ExpiresAt);
    }

    public async Task<UserSessionDto> CompleteMfaLoginAsync(
        CompleteMfaLoginDto body, AuthSessionService sessions, CancellationToken ct)
    {
        Validate(body);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var challenge = await LoadAsync(body.ChallengeId, "PASSWORD_MFA", ct);
        var suppliedHash = AuthSessionService.HashRefreshToken(body.ChallengeToken);
        if (challenge.ExpiresAt <= clock.GetUtcNow() || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(challenge.CodeHash), Encoding.UTF8.GetBytes(suppliedHash)))
            throw Error("MFA_CHALLENGE_INVALID", "Sign-in verification expired or is invalid. Sign in again.", 401);

        var user = await LoadActiveMfaUserAsync(challenge.UserId, ct);
        var credential = await LoadMfaCredentialAsync(challenge.UserId, ct);
        if (credential?.Enabled != true)
            throw Error("MFA_CHALLENGE_INVALID", "Security settings changed. Sign in again.", 401);

        await VerifyMfaAsync(credential, body.Totp, body.RecoveryCode, ct);
        challenge.ConsumedAt = clock.GetUtcNow();
        challenge.CodeHash = "";
        await db.SaveChangesAsync(ct);
        var session = await sessions.CreateAsync(user, ct);
        if (!string.IsNullOrWhiteSpace(body.RecoveryCode))
            session.RecoveryCodesRemaining = await RemainingRecoveryCodesAsync(challenge.UserId, ct);
        await transaction.CommitAsync(ct);
        return session;
    }

    public async Task<MfaSetupDto> SetupMfaAsync(Guid id, MfaVerificationDto body, CancellationToken ct)
    {
        Validate(body);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var user = await LoadActiveMfaUserAsync(id, ct);
        var credential = await LoadMfaCredentialAsync(id, ct) ?? new MfaCredential { UserId = id };
        await VerifySecurityChangeAsync(user, credential, body, ct);

        var secret = Base32(RandomNumberGenerator.GetBytes(20));
        credential.PendingProtectedSecret = protector.Protect(secret);
        credential.PendingExpiresAt = clock.GetUtcNow().AddMinutes(10);
        if (db.IsNewCredential(credential))
            db.Stage(credential);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var label = Uri.EscapeDataString(user.Email ?? user.Phone!);
        var uri = $"otpauth://totp/SmartPark:{label}?secret={secret}&issuer=SmartPark&digits=6&period=30";
        return new(secret, uri, credential.PendingExpiresAt.Value);
    }

    public async Task<MfaRecoveryCodesDto> EnableMfaAsync(Guid id, string code, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var user = await LoadActiveMfaUserAsync(id, ct);
        var credential = await LoadMfaCredentialAsync(id, ct);
        if (credential?.PendingProtectedSecret is null || credential.PendingExpiresAt <= clock.GetUtcNow())
            throw Error("MFA_SETUP_EXPIRED", "Start authenticator setup again.");
        EnsureMfaUnlocked(credential);

        var pending = new MfaCredential { ProtectedSecret = credential.PendingProtectedSecret };
        if (!CheckTotp(pending, code))
            await RejectMfaAsync(credential, ct);

        var replacing = credential.Enabled;
        credential.ProtectedSecret = credential.PendingProtectedSecret;
        credential.PendingProtectedSecret = null;
        credential.PendingExpiresAt = null;
        credential.Enabled = true;
        credential.LastStep = pending.LastStep;
        credential.Attempts = 0;
        credential.LockedUntil = null;
        var codes = await ReplaceRecoveryCodesAsync(id, ct);
        await CloseLoginChallengesAsync(id, ct);
        if (replacing)
            await RevokeMfaSessionsAsync(id, ct);
        await NotifyMfaChangeAsync(user, replacing ? "Authenticator replaced" : "Two-factor authentication enabled", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(codes, replacing);
    }

    public async Task DisableMfaAsync(Guid id, MfaVerificationDto body, CancellationToken ct)
    {
        Validate(body);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var user = await LoadActiveMfaUserAsync(id, ct);
        var credential = await RequireEnabledMfaAsync(id, ct);
        await VerifySecurityChangeAsync(user, credential, body, ct);
        db.RemoveCredential(credential);
        await db.DeleteRecoveryCodesAsync(id, ct);
        DetachRecoveryCodes();
        await CloseLoginChallengesAsync(id, ct);
        await RevokeMfaSessionsAsync(id, ct);
        await NotifyMfaChangeAsync(user, "Two-factor authentication disabled", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<MfaRecoveryCodesDto> RegenerateRecoveryCodesAsync(
        Guid id, MfaVerificationDto body, CancellationToken ct)
    {
        Validate(body);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var user = await LoadActiveMfaUserAsync(id, ct);
        var credential = await RequireEnabledMfaAsync(id, ct);
        await VerifySecurityChangeAsync(user, credential, body, ct);
        var codes = await ReplaceRecoveryCodesAsync(id, ct);
        await CloseLoginChallengesAsync(id, ct);
        await NotifyMfaChangeAsync(user, "Recovery codes replaced", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(codes, false);
    }

    private async Task<User> LoadActiveMfaUserAsync(Guid id, CancellationToken ct)
    {
        var user = await db.LockUserWithRolesAsync(id, ct);
        if (user.DeletedOn is not null || user.Status != UserStatus.Active || AccountRolePolicy.CurrentRole(user) is null)
            throw Error("AUTH_FAILED", "Account access is blocked.", 401);
        return user;
    }

    private Task<MfaCredential?> LoadMfaCredentialAsync(Guid id, CancellationToken ct) =>
        db.LockMfaCredentialAsync(id, ct);

    private async Task<MfaCredential> RequireEnabledMfaAsync(Guid id, CancellationToken ct)
    {
        var credential = await LoadMfaCredentialAsync(id, ct);
        if (credential?.Enabled != true)
            throw Error("MFA_NOT_ENABLED", "Two-factor authentication is not enabled.", 409);
        return credential;
    }

    private async Task VerifySecurityChangeAsync(
        User user, MfaCredential credential, MfaVerificationDto body, CancellationToken ct)
    {
        var policy = authenticationPolicy ?? new AuthenticationPolicy();
        if (!passwords.Verify(body.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= policy.MaxFailedLoginAttempts)
            {
                user.Status = UserStatus.Locked;
                user.LockedUntil = clock.GetUtcNow().AddMinutes(policy.LockoutMinutes);
            }
            await CommitMfaFailureAsync(ct);
            throw Error("AUTH_FAILED", "Password verification failed.", 401);
        }
        user.FailedLoginAttempts = 0;
        if (credential.Enabled)
            await VerifyMfaAsync(credential, body.Totp, body.RecoveryCode, ct);
    }

    private void EnsureMfaUnlocked(MfaCredential credential)
    {
        if (credential.LockedUntil > clock.GetUtcNow())
            throw Error("MFA_LOCKED", "Too many invalid codes. Try again after 15 minutes.", 423);
        if (credential.LockedUntil is not null)
        {
            credential.LockedUntil = null;
            credential.Attempts = 0;
        }
    }

    private async Task VerifyMfaAsync(MfaCredential credential, string? totp, string? recoveryCode, CancellationToken ct)
    {
        EnsureMfaUnlocked(credential);
        if (string.IsNullOrWhiteSpace(totp) && string.IsNullOrWhiteSpace(recoveryCode))
            throw Error("MFA_REQUIRED", "Enter an authenticator code or a recovery code.", 401);
        if (!string.IsNullOrWhiteSpace(totp) && !string.IsNullOrWhiteSpace(recoveryCode))
            throw Error("VALIDATION_FAILED", "Use only one verification method.");

        if (!string.IsNullOrWhiteSpace(recoveryCode))
        {
            var hash = AuthSessionService.HashRefreshToken(NormalizeRecoveryCode(recoveryCode));
            var stored = await db.FindUnusedRecoveryCodeAsync(credential.UserId, hash, ct);
            if (stored is not null)
            {
                stored.UsedAt = clock.GetUtcNow();
                credential.Attempts = 0;
                return;
            }
        }
        else if (CheckTotp(credential, totp))
        {
            credential.Attempts = 0;
            return;
        }
        await RejectMfaAsync(credential, ct);
    }

    private async Task RejectMfaAsync(MfaCredential credential, CancellationToken ct)
    {
        credential.Attempts++;
        if (credential.Attempts >= 3)
            credential.LockedUntil = clock.GetUtcNow().AddMinutes(15);
        await CommitMfaFailureAsync(ct);
        throw Error(credential.LockedUntil is null ? "MFA_INVALID" : "MFA_LOCKED",
            credential.LockedUntil is null ? "Invalid or already used verification code." : "Too many invalid codes. Try again after 15 minutes.",
            credential.LockedUntil is null ? 401 : 423);
    }

    // Persist failed attempts before throwing; otherwise transaction disposal would
    // roll back the counters and let a caller retry indefinitely.
    private async Task CommitMfaFailureAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await db.CommitCurrentTransactionAsync(ct);
    }

    private bool CheckTotp(MfaCredential credential, string? code)
    {
        var step = clock.GetUtcNow().ToUnixTimeSeconds() / 30;
        for (var offset = -1; offset <= 1; offset++)
        {
            var candidate = step + offset;
            if (candidate <= credential.LastStep)
                continue;
            var expected = Totp(protector.Unprotect(credential.ProtectedSecret), candidate);
            if (code is not null && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes(expected)))
            {
                credential.LastStep = candidate;
                return true;
            }
        }
        return false;
    }

    public static string NormalizeRecoveryCode(string code) => code.Trim().Replace("-", "").ToUpperInvariant();

    private async Task<string[]> ReplaceRecoveryCodesAsync(Guid id, CancellationToken ct)
    {
        await db.DeleteRecoveryCodesAsync(id, ct);
        // A recovery code may have been consumed earlier in this same transaction.
        DetachRecoveryCodes();
        var codes = new string[RecoveryCodeCount];
        for (var index = 0; index < codes.Length; index++)
        {
            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            codes[index] = string.Join("-", Enumerable.Range(0, 4).Select(group => raw.Substring(group * 8, 8)));
            db.Stage(new MfaRecoveryCode
            {
                Id = Guid.NewGuid(), UserId = id,
                CodeHash = AuthSessionService.HashRefreshToken(raw)
            });
        }
        return codes;
    }

    private void DetachRecoveryCodes()
    {
        db.ForgetRecoveryCodes();
    }

    private Task<int> CloseLoginChallengesAsync(Guid id, CancellationToken ct) =>
        db.CloseLoginChallengesAsync(id, clock.GetUtcNow(), ct);

    private Task<int> RevokeMfaSessionsAsync(Guid id, CancellationToken ct) =>
        db.RevokeMfaSessionsAsync(id, ct);

    private async Task NotifyMfaChangeAsync(User user, string action, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(user.Email) && user.EmailVerifiedAt is not null)
            await QueueAsync(user.Id!.Value, user.Id.Value, "MFA_SECURITY_CHANGE",
                new("email", user.Email, "SmartPark security notification",
                    $"{action} at {clock.GetUtcNow():u}. If you did not make this change, contact support."), ct);
    }

    public static string Totp(string secret, long step)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var buffer = 0;
        var bytes = new List<byte>();
        foreach (var character in secret)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter);
        var position = hash[^1] & 15;
        var value = ((hash[position] & 127) << 24) | (hash[position + 1] << 16) |
            (hash[position + 2] << 8) | hash[position + 3];
        return (value % 1_000_000).ToString("D6");
    }

    private static string Base32(byte[] value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        var bits = 0;
        var buffer = 0;
        foreach (var item in value)
        {
            buffer = (buffer << 8) | item;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                result.Append(alphabet[(buffer >> bits) & 31]);
            }
        }
        if (bits > 0)
            result.Append(alphabet[(buffer << (5 - bits)) & 31]);
        return result.ToString();
    }
}
