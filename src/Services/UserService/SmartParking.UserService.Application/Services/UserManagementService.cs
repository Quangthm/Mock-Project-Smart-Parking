using UserService.Application.Common.Interfaces.Persistence;
using System.ComponentModel.DataAnnotations;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Application.Services;

public sealed class UserManagementService(IIdentityWorkflowStore db, TimeProvider clock) : IUserManagementService
{
    private static AuthException Error(string code, string message, int status = 400) => new(code, message, status);
    private static void Validate(object body)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(body, new ValidationContext(body), errors, true))
            throw Error("VALIDATION_FAILED", string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }
    private static string? Role(string code) => code.ToUpperInvariant() switch
    {
        "PLATFORM_ADMIN" or "ADMIN" => "admin", "BUSINESS_OWNER" or "OWNER" => "owner",
        "SITE_OPERATOR" or "OPERATOR" => "operator", "DRIVER" => "driver", _ => null
    };
    private static ManagedUserDto Result(User u) => new(u.Id!.Value, u.FullName, u.Email, u.Phone,
        u.Accounts.Where(a => a.DeletedOn == null).SelectMany(a => a.AccountRoles)
            .Select(r => Role(r.RoleCode)).OfType<string>().Distinct().Order().ToArray(),
        char.ToLowerInvariant(u.Status.ToString()[0]) + u.Status.ToString()[1..], u.CreatedOn, u.LockedUntil);

    private async Task RequireAdmin(Guid id, CancellationToken ct)
    {
        if (!await db.IsActiveAdminAsync(id, ct))
            throw Error("FORBIDDEN", "Active Admin access is required.", 403);
    }
    public async Task<ManagedUsersPage> ListAsync(Guid adminId, UserSearchDto query, CancellationToken ct)
    {
        Validate(query);
        await RequireAdmin(adminId, ct);
        var (items, total) = await db.SearchUsersAsync(query, ct);
        return new(items.Select(Result).ToList(), total, query.Page, query.PageSize);
    }
    public async Task<ManagedUserDetail> GetAsync(Guid adminId, Guid id, CancellationToken ct)
    {
        await RequireAdmin(adminId, ct);
        var user = await db.FindManagedUserAsync(id, ct)
            ?? throw Error("USER_NOT_FOUND", "User was not found.", 404);
        var activity = await db.ReadLoginActivityAsync(id, ct);
        return new(Result(user), activity);
    }
    public async Task<ManagedUserDto> SetStatusAsync(Guid adminId, Guid id, UserStatusDto body, CancellationToken ct)
    {
        Validate(body);
        await using var tx = await db.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        var user = await db.LockOptionalUserAsync(id, ct);
        if (user == null || user.DeletedOn != null) throw Error("USER_NOT_FOUND", "User was not found.", 404);
        await db.LoadUserRolesAsync(user, ct);
        if (id == adminId || Result(user).Roles.Contains("admin"))
            throw Error("ADMIN_ACCOUNT_PROTECTED", "Admin accounts cannot be changed through user management.", 409);
        if (user.Status != UserStatus.Active && user.Status != UserStatus.Locked)
            throw Error("INVALID_STATUS_TRANSITION", "Verification or owner approval must complete before managing access.", 409);
        user.Status = body.Status == "locked" ? UserStatus.Locked : UserStatus.Active;
        if (user.Status == UserStatus.Active) OperatorActivation.RequireVerification(user);
        user.LockedUntil = null; // An Admin lock stays in effect until explicitly unlocked.
        user.FailedLoginAttempts = 0;
        user.ModifiedOn = clock.GetUtcNow();
        if (user.Status == UserStatus.Locked)
            await db.RevokeActiveSessionsAsync(id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result(user);
    }
}
