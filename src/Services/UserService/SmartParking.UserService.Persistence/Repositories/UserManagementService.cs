using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Persistence.Repositories;

public sealed class UserManagementService(AppDbContext db, TimeProvider clock) : IUserManagementService
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
    private IQueryable<User> Users => db.Users.Where(u => u.DeletedOn == null)
        .Include(u => u.Accounts).ThenInclude(a => a.AccountRoles);
    private async Task RequireAdmin(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id && u.DeletedOn == null && u.Status == UserStatus.Active &&
            u.Accounts.Any(a => a.DeletedOn == null && a.Status == "ACTIVE" &&
                a.AccountRoles.Any(r => r.RoleCode == "PLATFORM_ADMIN" || r.RoleCode == "ADMIN")), ct))
            throw Error("FORBIDDEN", "Active Admin access is required.", 403);
    }
    public async Task<ManagedUsersPage> ListAsync(Guid adminId, UserSearchDto query, CancellationToken ct)
    {
        Validate(query);
        await RequireAdmin(adminId, ct);
        var users = Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            users = users.Where(u => u.FullName.ToLower().Contains(search) ||
                (u.Email != null && u.Email.ToLower().Contains(search)) || (u.Phone != null && u.Phone.Contains(search)));
        }
        if (query.Status != null)
        {
            var status = Enum.Parse<UserStatus>(query.Status, true);
            users = users.Where(u => u.Status == status);
        }
        if (query.Role != null)
        {
            string[] codes = query.Role switch { "admin" => ["PLATFORM_ADMIN", "ADMIN"],
                "owner" => ["BUSINESS_OWNER", "OWNER"], "operator" => ["SITE_OPERATOR", "OPERATOR"], _ => ["DRIVER"] };
            users = users.Where(u => u.Accounts.Any(a => a.DeletedOn == null && a.AccountRoles.Any(r => codes.Contains(r.RoleCode))));
        }
        var total = await users.CountAsync(ct);
        var offset = (long)(query.Page - 1) * query.PageSize;
        var items = offset > int.MaxValue ? new List<User>() : await users.OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(Result).ToList(), total, query.Page, query.PageSize);
    }
    public async Task<ManagedUserDetail> GetAsync(Guid adminId, Guid id, CancellationToken ct)
    {
        await RequireAdmin(adminId, ct);
        var user = await Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw Error("USER_NOT_FOUND", "User was not found.", 404);
        var activity = await db.AuthSessions.AsNoTracking().Where(s => s.UserId == id).OrderByDescending(s => s.CreatedAt)
            .Take(50).Select(s => new UserLoginActivity(s.CreatedAt, s.ExpiresAt, s.IsRevoked)).ToListAsync(ct);
        return new(Result(user), activity);
    }
    public async Task<ManagedUserDto> SetStatusAsync(Guid adminId, Guid id, UserStatusDto body, CancellationToken ct)
    {
        Validate(body);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequireAdmin(adminId, ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user == null || user.DeletedOn != null) throw Error("USER_NOT_FOUND", "User was not found.", 404);
        await db.Entry(user).Collection(u => u.Accounts).Query().Include(a => a.AccountRoles).LoadAsync(ct);
        if (id == adminId || Result(user).Roles.Contains("admin"))
            throw Error("ADMIN_ACCOUNT_PROTECTED", "Admin accounts cannot be changed through user management.", 409);
        if (user.Status != UserStatus.Active && user.Status != UserStatus.Locked)
            throw Error("INVALID_STATUS_TRANSITION", "Verification or owner approval must complete before managing access.", 409);
        user.Status = body.Status == "locked" ? UserStatus.Locked : UserStatus.Active;
        user.LockedUntil = null; // An Admin lock stays in effect until explicitly unlocked.
        user.FailedLoginAttempts = 0;
        user.ModifiedOn = clock.GetUtcNow();
        if (user.Status == UserStatus.Locked)
            await db.AuthSessions.Where(s => s.UserId == id && !s.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(s => s.IsRevoked, true), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result(user);
    }
}
