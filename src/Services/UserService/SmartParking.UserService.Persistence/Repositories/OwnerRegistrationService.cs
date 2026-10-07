using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Persistence.Repositories;

public sealed class OwnerRegistrationService(AppDbContext db, IPasswordService passwords, TimeProvider clock, IParkingDirectory parking)
    : IOwnerRegistrationService
{
    private static AuthException Error(string code, string message, int status = 400) => new(code, message, status);
    private static void Validate(object body)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(body, new ValidationContext(body), errors, true))
            throw Error("VALIDATION_FAILED", string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }
    private static OwnerApplicationDto Result(OwnerApplication a) => new(a.Id, a.UserId, a.User.FullName,
        a.BusinessName, a.User.Email!, a.User.Phone!, a.LotType, a.Status, a.SubmittedAt,
        a.ReviewedAt, a.ReviewedBy, a.ReviewNote);

    public async Task<OwnerApplicationDto> RegisterAsync(OwnerRegistrationDto body, CancellationToken ct)
    {
        Validate(body);
        var email = body.Email.Trim().ToLowerInvariant();
        var phone = body.Phone.Trim();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Users.AnyAsync(u => u.DeletedOn == null &&
            ((u.Email != null && u.Email.ToLower() == email) || u.Phone == phone), ct))
            throw Error("CONTACT_EXISTS", "Email or phone is already registered.", 409);
        var now = clock.GetUtcNow();
        var user = new User { Id = Guid.NewGuid(), FullName = body.FullName.Trim(), CompanyName = body.BusinessName.Trim(), Email = email, Phone = phone,
            PasswordHash = passwords.Hash(body.Password), Status = UserStatus.PendingApproval, CreatedOn = now, ModifiedOn = now };
        var account = new Account { Id = Guid.NewGuid(), User = user, UserId = user.Id.Value,
            Status = "PENDING_APPROVAL", CreatedOn = now };
        account.AccountRoles.Add(new AccountRole { Account = account, AccountId = account.Id.Value, RoleCode = "BUSINESS_OWNER" });
        user.Accounts.Add(account);
        var application = new OwnerApplication { Id = Guid.NewGuid(), UserId = user.Id.Value, User = user,
            BusinessName = body.BusinessName.Trim(), LotType = body.LotType, SubmittedAt = now };
        db.OwnerApplications.Add(application);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        { throw Error("CONTACT_EXISTS", "Email or phone is already registered.", 409); }
        await tx.CommitAsync(ct);
        return Result(application);
    }

    private async Task RequireAdmin(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id && u.DeletedOn == null && u.Status == UserStatus.Active &&
            u.Accounts.Any(a => a.DeletedOn == null && a.Status == "ACTIVE" &&
                a.AccountRoles.Any(r => r.RoleCode == "PLATFORM_ADMIN" || r.RoleCode == "ADMIN")), ct))
            throw Error("FORBIDDEN", "Active Admin access is required.", 403);
    }

    public async Task<IReadOnlyList<OwnerApplicationDto>> ListAsync(Guid adminId, CancellationToken ct)
    {
        await RequireAdmin(adminId, ct);
        var applications = await db.OwnerApplications.AsNoTracking().Include(a => a.User)
            .Where(a => a.User.DeletedOn == null).OrderByDescending(a => a.SubmittedAt).ToListAsync(ct);
        return applications.Select(Result).ToList();
    }

    public async Task<OwnerApplicationDto> ReviewAsync(Guid adminId, Guid id, ReviewOwnerDto body, CancellationToken ct)
    {
        Validate(body);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        // Serialize competing reviews; only the first pending -> final decision may commit.
        var application = await db.OwnerApplications.FromSqlInterpolated(
            $"SELECT * FROM owner_applications WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (application is null) throw Error("APPLICATION_NOT_FOUND", "Owner application was not found.", 404);
        var user = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {application.UserId} FOR UPDATE").SingleAsync(ct);
        await db.Entry(user).Collection(u => u.Accounts).Query().Include(a => a.AccountRoles).LoadAsync(ct);
        if (application.Status != "pending" || user.DeletedOn != null || user.Status != UserStatus.PendingApproval)
            throw Error("APPLICATION_CLOSED", "Owner application is no longer pending.", 409);
        var account = user.Accounts.SingleOrDefault(a => a.DeletedOn == null && a.Status == "PENDING_APPROVAL" &&
            a.AccountRoles.Any(r => r.RoleCode == "BUSINESS_OWNER"));
        if (account is null) throw Error("APPLICATION_CLOSED", "Owner account is no longer eligible for review.", 409);
        application.User = user;
        application.Status = body.Status;
        application.ReviewedBy = adminId;
        application.ReviewedAt = clock.GetUtcNow();
        application.ReviewNote = body.ReviewNote?.Trim();
        user.Status = body.Status == "approved" ? UserStatus.Active : UserStatus.Rejected;
        user.ModifiedOn = clock.GetUtcNow();
        if (body.Status == "approved")
        {
            // Deterministic ID makes remote provisioning retryable if the local commit fails.
            var tenantId = application.Id;
            await parking.ProvisionTenantAsync(tenantId, application.BusinessName, user.Email!, user.Phone!, ct);
            account.TenantId = tenantId;
            account.Status = "ACTIVE";
        }
        else account.Status = "INACTIVE";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result(application);
    }
}
