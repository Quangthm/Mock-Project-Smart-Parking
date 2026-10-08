using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using System.Security.Cryptography;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Persistence.Repositories;

public sealed class DriverRegistrationService(AppDbContext db, IPasswordService passwords,
    IOtpSender sender, TimeProvider clock) : IDriverRegistrationService
{
    private static AuthException Error(string code, string message, int status = 400) => new(code, message, status);
    private static string NewCode() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
    private DriverRegistrationResult Result(DriverRegistration r) => new(r.Id, r.Channel, r.ExpiresAt, r.ResendAvailableAt);
    private void SetCode(DriverRegistration r, string code)
    {
        // A slow, salted hash protects the small OTP keyspace against offline guessing.
        r.CodeHash = passwords.Hash(code);
        r.ExpiresAt = clock.GetUtcNow().AddMinutes(5);
        r.ResendAvailableAt = clock.GetUtcNow().AddSeconds(60);
    }

    public async Task<DriverRegistrationResult> RegisterAsync(DriverRegistrationDto request, CancellationToken ct)
    {
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        AccountWorkflowService.Validate(request);
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : AccountWorkflowService.Contact(request.Phone);
        if (email is null && phone is null || string.IsNullOrWhiteSpace(request.FullName))
            throw Error("VALIDATION_FAILED", "Full name and email or phone are required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Users.AnyAsync(u => u.DeletedOn == null &&
            ((email != null && u.Email != null && u.Email.ToLower() == email) || (phone != null && u.Phone == phone)), ct))
            throw Error("CONTACT_EXISTS", "Email or phone is already registered. Resume OTP verification if pending.", 409);
        var now = clock.GetUtcNow();
        var user = new User { Id = Guid.NewGuid(), FullName = request.FullName.Trim(), Email = email, Phone = phone,
            PasswordHash = string.IsNullOrEmpty(request.Password) ? "" : passwords.Hash(request.Password), Status = UserStatus.PendingVerification, CreatedOn = now, ModifiedOn = now };
        var account = new Account { Id = Guid.NewGuid(), User = user, UserId = user.Id.Value,
            CreatedOn = now, Status = "ACTIVE" };
        account.AccountRoles.Add(new AccountRole { Account = account, AccountId = account.Id.Value, RoleCode = "DRIVER" });
        user.Accounts.Add(account);
        var registration = new DriverRegistration { Id = Guid.NewGuid(), UserId = user.Id.Value, Channel = email != null ? "email" : "sms" };
        var code = NewCode();
        SetCode(registration, code);
        db.Users.Add(user);
        db.DriverRegistrations.Add(registration);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        { throw Error("CONTACT_EXISTS", "Email or phone is already registered.", 409); }
        await sender.SendAsync(registration.Channel, email ?? phone!, code, ct);
        await tx.CommitAsync(ct);
        return Result(registration);
    }
    public async Task<DriverRegistrationResult> RecoverAsync(string contact,CancellationToken ct)
    {
        contact=AccountWorkflowService.Contact(contact);
        var user=await db.Users.SingleOrDefaultAsync(u=>u.DeletedOn==null && u.Status==UserStatus.PendingVerification && (u.Email==contact || u.Phone==contact),ct);
        var challenge=user is null?null:await db.DriverRegistrations.SingleOrDefaultAsync(r=>r.UserId==user.Id && r.VerifiedAt==null,ct);
        if(challenge is null)throw Error("REGISTRATION_NOT_FOUND","Pending registration was not found.",404);
        return Result(challenge);
    }

    private async Task<(DriverRegistration Registration, User User)> LoadAsync(Guid id, CancellationToken ct)
    {
        var r = await db.DriverRegistrations.FromSqlInterpolated(
            $"SELECT * FROM driver_registrations WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (r is null) throw Error("REGISTRATION_NOT_FOUND", "Registration was not found.", 404);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {r.UserId} FOR UPDATE").SingleAsync(ct);
        if (r.VerifiedAt != null || user.Status != UserStatus.PendingVerification || user.DeletedOn != null)
            throw Error("REGISTRATION_CLOSED", "Registration is no longer pending.", 409);
        if (r.LockedUntil > clock.GetUtcNow())
            throw Error("OTP_LOCKED", "Too many incorrect codes. Try again after 15 minutes.", 423);
        if (r.LockedUntil != null) { r.LockedUntil = null; r.FailedAttempts = 0; }
        return (r, user);
    }

    public async Task VerifyAsync(VerifyDriverOtpDto request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (r, user) = await LoadAsync(request.RegistrationId, ct);
        if (r.ExpiresAt <= clock.GetUtcNow()) throw Error("OTP_EXPIRED", "Code has expired. Request a new code.");
        if (!passwords.Verify(request.Code, r.CodeHash))
        {
            r.FailedAttempts++;
            if (r.FailedAttempts >= 3) r.LockedUntil = clock.GetUtcNow().AddMinutes(15);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw r.LockedUntil != null ? Error("OTP_LOCKED", "Too many incorrect codes. Try again after 15 minutes.", 423)
                : Error("OTP_INVALID", "Incorrect verification code.");
        }
        r.VerifiedAt = clock.GetUtcNow();
        r.CodeHash = "";
        r.FailedAttempts = 0;
        user.Status = UserStatus.Active;
        if(r.Channel=="email")user.EmailVerifiedAt=clock.GetUtcNow();else user.PhoneVerifiedAt=clock.GetUtcNow();
        user.ModifiedOn = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<DriverRegistrationResult> ResendAsync(Guid registrationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (r, user) = await LoadAsync(registrationId, ct);
        if (r.ResendAvailableAt > clock.GetUtcNow()) throw Error("OTP_COOLDOWN", "Wait 60 seconds before requesting another code.", 429);
        var code = NewCode();
        while (passwords.Verify(code, r.CodeHash)) code = NewCode();
        SetCode(r, code); // Resending never resets failed attempts or bypasses the lock.
        await db.SaveChangesAsync(ct);
        await sender.SendAsync(r.Channel, r.Channel == "email" ? user.Email! : user.Phone!, code, ct);
        await tx.CommitAsync(ct);
        return Result(r);
    }
}
