using Microsoft.Extensions.Logging;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Authorization;

/// <summary>Existing action and scope rules, evaluated only after the bound session's current-access guard.</summary>
public sealed class AccountAuthorizationService(ICurrentAccessService current, IAuthorizationScopeStore scopes,
    IParkingDirectory parking, ILogger<AccountAuthorizationService> logger) : IAccountAuthorizationService
{
    private AuthException Denied(CurrentAccessContext actor, string decision, Guid? resourceId = null)
    {
        logger.LogInformation("Authorization denied for account {AccountId}; decision {Decision}; resource {ResourceId}.",
            actor.Account.Id, decision, resourceId);
        return new("FORBIDDEN", "Current role, permission and resource access are required.", 403);
    }

    private async Task<CurrentAccessContext> RequireRoleAsync(Guid? userId, string role, bool lockRows, CancellationToken ct)
    {
        var actor = lockRows ? await current.LockAsync(ct) : await current.RequireAsync(ct);
        if (actor.Role != role || userId is { } id && actor.User.Id != id)
            throw Denied(actor, "ROLE_OR_ACTOR");
        return actor;
    }

    private static bool Eligible(AccountMembership membership, CurrentAccessContext actor) =>
        membership.UserId == actor.User.Id && membership.Status == "ACTIVE" && membership.DeletedAt is null
        && AccessRoles.Map(membership.RoleCode) == actor.Role;

    private static bool HasOwnerPermission(AccountMembership membership, string permission) =>
        membership.Permissions is null ? permission != "BACKUP_MARK" : membership.Permissions.Contains(permission, StringComparer.Ordinal);

    public Task<CurrentAccessContext> RequireAdminAsync(Guid userId, bool lockRows, CancellationToken ct) =>
        RequireRoleAsync(userId, "admin", lockRows, ct);

    public async Task<CurrentAccessContext> RequireOwnerReviewAsync(Guid userId, bool lockRows, CancellationToken ct)
    {
        var actor = await RequireRoleAsync(userId, "admin", lockRows, ct);
        // User management historically requires the Admin role. Owner review additionally
        // requires ACCOUNT_ADMIN; keep that distinction until its baseline is explicitly changed.
        if (actor.Account.Permissions is { } permissions
            && !permissions.Contains("ACCOUNT_ADMIN", StringComparer.Ordinal))
            throw Denied(actor, "ACCOUNT_ADMIN");
        return actor;
    }

    private async Task<IdentityScope> OwnerAsync(Guid? userId, string permission, bool lockRows, CancellationToken ct)
    {
        var actor = await RequireRoleAsync(userId, "owner", lockRows, ct);
        var memberships = await scopes.LoadAsync(actor.User.Id!.Value, lockRows, ct);
        var tenants = memberships.Where(m => Eligible(m, actor) && m.RoleCode == "BUSINESS_OWNER"
                && m.SiteId is null && m.TenantId is not null
                && HasOwnerPermission(m, permission))
            .Select(m => m.TenantId!.Value).Distinct().ToArray();
        if (tenants.Length == 0) throw Denied(actor, permission);
        return new(actor.User.Id.Value, tenants);
    }

    public Task<IdentityScope> RequireOwnerManagementAsync(Guid userId, bool lockRows, CancellationToken ct) =>
        OwnerAsync(userId, "OPERATOR_MANAGE", lockRows, ct);

    public async Task<IReadOnlyList<DirectorySite>> RequireManagedSitesAsync(Guid userId, Guid[] siteIds, CancellationToken ct)
    {
        var scope = await RequireOwnerManagementAsync(userId, true, ct);
        var sites = await parking.ActiveSitesAsync(siteIds, scope.TenantIds, ct);
        if (sites.Count != siteIds.Length || sites.Any(s => !scope.TenantIds.Contains(s.TenantId)))
        {
            logger.LogInformation("Operator provisioning denied for user {UserId}; decision {Decision}.", userId, "SITE_ACCESS_DENIED");
            throw new AuthException("SITE_ACCESS_DENIED", "Every assigned site must be active and owned by this Owner.", 403);
        }
        return sites;
    }

    public Task<IdentityScope> OwnerScopeAsync(OwnerScopeAction action, CancellationToken ct)
    {
        var permission = action switch
        {
            OwnerScopeAction.ParkingManage => "PARKING_MANAGE",
            OwnerScopeAction.BackupConfigure => "BACKUP_CONFIGURE",
            OwnerScopeAction.BackupMark => "BACKUP_MARK",
            _ => throw new AuthException("VALIDATION_FAILED", "Unsupported owner-scope permission.", 400)
        };
        return OwnerAsync(null, permission, false, ct);
    }

    public async Task<IReadOnlyList<OperatorAssignment>> OperatorAssignmentsAsync(Guid userId, CancellationToken ct)
    {
        var actor = await RequireRoleAsync(userId, "operator", false, ct);
        return await AssignmentsAsync(actor, ct);
    }

    private async Task<IReadOnlyList<OperatorAssignment>> AssignmentsAsync(CurrentAccessContext actor, CancellationToken ct)
    {
        var memberships = await scopes.LoadAsync(actor.User.Id!.Value, false, ct);
        var assigned = memberships.Where(m => Eligible(m, actor) && m.RoleCode == "SITE_OPERATOR"
            && m.TenantId is not null && m.SiteId is not null
            && m.OperatorPermissions is not null).ToArray();
        if (assigned.Length == 0) return [];
        var active = await parking.ActiveSitesAsync(assigned.Select(m => m.SiteId!.Value).Distinct().ToArray(),
            assigned.Select(m => m.TenantId!.Value).Distinct().ToArray(), ct);
        return assigned.Where(m => active.Any(s => s.Id == m.SiteId && s.TenantId == m.TenantId))
            .Select(m => new OperatorAssignment(m.SiteId!.Value, m.TenantId!.Value, m.OperatorPermissions!)).ToArray();
    }

    public async Task<IdentityScope> OperatorScopeAsync(Guid siteId, OperatorScopeAction action, CancellationToken ct)
    {
        var actor = await RequireRoleAsync(null, "operator", false, ct);
        var permission = action switch
        {
            OperatorScopeAction.AssignedSite => null,
            OperatorScopeAction.SlotOverride => "SLOT_OVERRIDE",
            _ => throw Denied(actor, "UNSUPPORTED_OPERATOR_PERMISSION", siteId)
        };
        var assignment = (await AssignmentsAsync(actor, ct)).SingleOrDefault(a => a.SiteId == siteId
            && (permission is null || a.Permissions.Contains(permission, StringComparer.Ordinal)));
        if (assignment is null) throw Denied(actor, permission ?? "SITE_ASSIGNMENT", siteId);
        return new(actor.User.Id!.Value, [assignment.TenantId]);
    }

    public async Task<Guid[]> DriverAccountsAsync(Guid userId, CancellationToken ct)
    {
        var actor = await RequireRoleAsync(userId, "driver", true, ct);
        var memberships = await scopes.LoadAsync(userId, true, ct);
        var accounts = memberships.Where(m => Eligible(m, actor) && m.RoleCode == "DRIVER")
            .Select(m => m.AccountId).Order().ToArray();
        if (accounts.Length == 0) throw Denied(actor, "DRIVER_MEMBERSHIP");
        return accounts;
    }

    public Task<CurrentAccessContext> RequireDriverAsync(Guid userId, bool lockRows, CancellationToken ct) =>
        RequireRoleAsync(userId, "driver", lockRows, ct);

    public async Task<CurrentAccessContext> RequireOwnSecurityAsync(Guid userId, CancellationToken ct)
    {
        var actor = await current.LockAsync(ct);
        if (actor.User.Id != userId) throw Denied(actor, "ACTOR_MISMATCH");
        return actor;
    }

    public async Task RequireDeliveryAsync(Guid userId, WorkflowDelivery delivery, CancellationToken ct)
    {
        var actor = await RequireOwnSecurityAsync(userId, ct);
        if (delivery.ActorId != userId)
            throw new AuthException("DELIVERY_NOT_FOUND", "Delivery was not found.", 404);
        if (delivery.Kind == "OPERATOR_ONBOARDING")
            await RequireOwnerManagementAsync(userId, true, ct);
        else
            await RequireOwnerReviewAsync(userId, true, ct);
    }
}
