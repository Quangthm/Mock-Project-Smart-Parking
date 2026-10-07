using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Persistence.Repositories;

public sealed class OperatorProvisioningService(AppDbContext db, IPasswordService passwords, TimeProvider clock, IParkingDirectory parking)
    : IOperatorProvisioningService
{
    private static AuthException Error(string code, string message, int status = 400) => new(code, message, status);

    public async Task<IReadOnlyList<OperatorAssignment>> AssignmentsAsync(Guid operatorId, CancellationToken ct)
    {
        var grants = await db.OperatorGrants.AsNoTracking().Where(g => g.Account.UserId == operatorId &&
            g.Account.User.Status == UserStatus.Active && g.Account.User.DeletedOn == null &&
            g.Account.Status == "ACTIVE" && g.Account.DeletedOn == null && g.Account.SiteId != null &&
            g.Account.TenantId != null && g.Account.AccountRoles.Any(r => r.RoleCode == "SITE_OPERATOR"))
            .Select(g => new OperatorAssignment(g.Account.SiteId!.Value, g.Account.TenantId!.Value, g.Permissions)).ToArrayAsync(ct);
        if (grants.Length == 0) return [];
        var sites = await parking.ActiveSitesAsync(grants.Select(g => g.SiteId).Distinct().ToArray(), grants.Select(g => g.TenantId).Distinct().ToArray(), ct);
        return grants.Where(g => sites.Any(s => s.Id == g.SiteId && s.TenantId == g.TenantId)).ToArray();
    }

    public async Task<IReadOnlyList<OperatorDto>> ListAsync(Guid ownerId, CancellationToken ct)
    {
        var tenants = await db.Accounts.Where(a => a.UserId == ownerId && a.User.Status == UserStatus.Active &&
            a.User.DeletedOn == null && a.Status == "ACTIVE" && a.DeletedOn == null && a.SiteId == null && a.TenantId != null &&
            a.AccountRoles.Any(r => r.RoleCode == "BUSINESS_OWNER")).Select(a => a.TenantId!.Value).ToArrayAsync(ct);
        if (tenants.Length == 0) throw Error("FORBIDDEN", "Active Owner tenant membership is required.", 403);
        var grants = await db.OperatorGrants.AsNoTracking().Include(g => g.Account).ThenInclude(a => a.User)
            .Where(g => g.CreatedBy == ownerId && g.Account.DeletedOn == null && g.Account.User.DeletedOn == null &&
                g.Account.TenantId != null && tenants.Contains(g.Account.TenantId.Value)).ToListAsync(ct);
        return grants.GroupBy(g => g.Account.UserId).Select(group =>
        {
            var user = group.First().Account.User;
            return new OperatorDto(user.Id!.Value, user.FullName, user.Email!, "operator", user.Status.ToString().ToLowerInvariant(),
                ownerId, group.Select(g => g.Account.SiteId!.Value).ToArray(), group.SelectMany(g => g.Permissions).Distinct().ToArray());
        }).OrderBy(o => o.FullName).ToArray();
    }

    public async Task<OperatorDto> CreateAsync(Guid ownerId, CreateOperatorDto body, CancellationToken ct)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(body, new ValidationContext(body), errors, true))
            throw Error("VALIDATION_FAILED", string.Join(" ", errors.Select(e => e.ErrorMessage)));
        var email = body.Email.Trim().ToLowerInvariant();
        var siteIds = body.SiteIds.Order().ToArray();
        var permissions = body.Permissions.Order(StringComparer.Ordinal).ToArray();
        var hash = passwords.Hash(body.Password);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Lock only identity-owned rows. Site/tenant state is validated via ParkingService.
        var ownerTenants = await db.Database.SqlQuery<Guid>($"""
            SELECT a.tenant_id AS "Value" FROM accounts a
            JOIN users u ON u.id = a.user_id JOIN account_roles r ON r.account_id = a.id
            WHERE u.id = {ownerId} AND u.status = 'ACTIVE' AND u.deleted_at IS NULL
            AND a.status = 'ACTIVE' AND a.deleted_at IS NULL AND a.tenant_id IS NOT NULL
            AND a.site_id IS NULL AND r.role_code = 'BUSINESS_OWNER'
            ORDER BY a.tenant_id, a.id FOR SHARE OF u, a, r
            """).ToListAsync(ct);
        if (ownerTenants.Count == 0) throw Error("FORBIDDEN", "Active Owner tenant membership is required.", 403);
        var sites = await parking.ActiveSitesAsync(siteIds, ownerTenants.Distinct().ToArray(), ct);
        if (sites.Count != siteIds.Length || sites.Any(s => !ownerTenants.Contains(s.TenantId)))
            throw Error("SITE_ACCESS_DENIED", "Every assigned site must be active and owned by this Owner.", 403);
        if (await db.Users.AnyAsync(u => u.DeletedOn == null && u.Email != null && u.Email.ToLower() == email, ct))
            throw Error("EMAIL_EXISTS", "Email is already registered.", 409);
        var now = clock.GetUtcNow();
        var user = new User { Id = Guid.NewGuid(), FullName = body.FullName.Trim(), Email = email,
            PasswordHash = hash, Status = UserStatus.Active, CreatedOn = now, ModifiedOn = now };
        foreach (var site in sites)
        {
            var account = new Account { Id = Guid.NewGuid(), User = user, UserId = user.Id.Value,
                TenantId = site.TenantId, SiteId = site.Id,
                Status = "ACTIVE", CreatedOn = now };
            account.AccountRoles.Add(new AccountRole { Account = account, AccountId = account.Id.Value, RoleCode = "SITE_OPERATOR" });
            user.Accounts.Add(account);
            db.OperatorGrants.Add(new OperatorGrant { AccountId = account.Id.Value, Account = account,
                CreatedBy = ownerId, CreatedAt = now, Permissions = permissions.ToArray() });
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        { throw Error("EMAIL_EXISTS", "Email is already registered.", 409); }
        await tx.CommitAsync(ct);
        return new(user.Id.Value, user.FullName, email, "operator", "active", ownerId, siteIds, permissions);
    }

    // Operational endpoints must check current grants with this method; JWT role alone conveys no site authority.
    public async Task<bool> HasPermissionAsync(Guid operatorId, Guid siteId, string permission, CancellationToken ct)
    {
        if (!CreateOperatorDto.DelegablePermissions.Contains(permission)) return false;
        var tenants = await db.Database.SqlQuery<Guid>($"""
            SELECT a.tenant_id AS "Value" FROM accounts a JOIN users u ON u.id = a.user_id
            JOIN account_roles r ON r.account_id = a.id JOIN operator_grants g ON g.account_id = a.id
            WHERE u.id = {operatorId} AND u.status = 'ACTIVE' AND u.deleted_at IS NULL
            AND a.status = 'ACTIVE' AND a.deleted_at IS NULL AND a.tenant_id IS NOT NULL
            AND a.site_id = {siteId} AND r.role_code = 'SITE_OPERATOR'
            AND {permission} = ANY(g.permissions)
            """).ToListAsync(ct);
        if (tenants.Count == 0) return false;
        var sites = await parking.ActiveSitesAsync([siteId], tenants.Distinct().ToArray(), ct);
        return sites.Count == 1 && sites[0].Id == siteId && tenants.Contains(sites[0].TenantId);
    }
}
