using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Persistence.Repositories;

public sealed class OperatorProvisioningService(AppDbContext db, IPasswordService passwords, TimeProvider clock)
    : IOperatorProvisioningService
{
    private static AuthException Error(string code, string message, int status = 400) => new(code, message, status);

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
        // Lock current user/memberships/roles/tenants. A concurrent revocation must wait for this creation.
        var ownerTenants = await db.Database.SqlQuery<Guid>($"""
            SELECT a.tenant_id AS "Value" FROM accounts a
            JOIN users u ON u.id = a.user_id JOIN account_roles r ON r.account_id = a.id
            JOIN tenants t ON t.id = a.tenant_id
            WHERE u.id = {ownerId} AND u.status = 'ACTIVE' AND u.deleted_at IS NULL
            AND a.account_type = 'BUSINESS_OPERATOR' AND a.status = 'ACTIVE' AND a.deleted_at IS NULL
            AND a.site_id IS NULL AND r.role_code = 'BUSINESS_OWNER'
            AND t.status = 'ACTIVE' AND t.deleted_at IS NULL
            ORDER BY a.tenant_id, a.id FOR SHARE OF u, a, r, t
            """).ToListAsync(ct);
        if (ownerTenants.Count == 0) throw Error("FORBIDDEN", "Active Owner tenant membership is required.", 403);
        var sites = await db.Database.SqlQuery<AssignedSite>($"""
            SELECT id AS "Id", tenant_id AS "TenantId" FROM parking_sites
            WHERE id = ANY({siteIds}) AND is_active AND deleted_at IS NULL
            ORDER BY id FOR SHARE
            """).ToListAsync(ct);
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
                AccountType = "BUSINESS_OPERATOR", TenantId = site.TenantId, SiteId = site.Id,
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
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT a.id AS "Value" FROM accounts a JOIN users u ON u.id = a.user_id
            JOIN account_roles r ON r.account_id = a.id JOIN operator_grants g ON g.account_id = a.id
            JOIN parking_sites s ON s.id = a.site_id AND s.tenant_id = a.tenant_id
            JOIN tenants t ON t.id = a.tenant_id
            WHERE u.id = {operatorId} AND u.status = 'ACTIVE' AND u.deleted_at IS NULL
            AND a.account_type = 'BUSINESS_OPERATOR' AND a.status = 'ACTIVE' AND a.deleted_at IS NULL
            AND a.site_id = {siteId} AND r.role_code = 'SITE_OPERATOR'
            AND {permission} = ANY(g.permissions) AND s.is_active AND s.deleted_at IS NULL
            AND t.status = 'ACTIVE' AND t.deleted_at IS NULL
            """).ToListAsync(ct);
        return ids.Count > 0;
    }

    private sealed class AssignedSite
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
    }
}
