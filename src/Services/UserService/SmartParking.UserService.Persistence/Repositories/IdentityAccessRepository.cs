using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

namespace UserService.Persistence.Repositories;

public sealed class IdentityAccessRepository(AppDbContext database) : IIdentityAccessRepository
{
    // The permission predicate is on the same membership that supplies the returned tenant.
    // Inside a command transaction these locks serialize grant revocation with the commit.
    public async Task<Guid[]> OwnerTenantsAsync(Guid actor, string permission, CancellationToken cancellationToken) =>
        (await database.Database.SqlQuery<Guid>($"""
            SELECT a.tenant_id AS "Value" FROM accounts a
            JOIN users u ON u.id=a.user_id JOIN account_roles r ON r.account_id=a.id
            WHERE u.id={actor} AND u.status='ACTIVE' AND u.deleted_at IS NULL
              AND a.status='ACTIVE' AND a.deleted_at IS NULL AND a.tenant_id IS NOT NULL
              AND a.site_id IS NULL AND r.role_code='BUSINESS_OWNER'
              AND {permission} IN ('OPERATOR_MANAGE','PARKING_MANAGE','BACKUP_CONFIGURE','BACKUP_MARK')
              AND ((a.permissions IS NULL AND {permission}<>'BACKUP_MARK') OR {permission}=ANY(a.permissions))
            ORDER BY a.id FOR SHARE OF u,a,r
            """).ToListAsync(cancellationToken)).Distinct().ToArray();

    public async Task<IReadOnlyList<OperatorDto>> ListOperatorsAsync(Guid actor, Guid[] tenants, CancellationToken cancellationToken)
    {
        var grants = await database.OperatorGrants.AsNoTracking().Include(grant => grant.Account).ThenInclude(a => a.User)
            .Where(grant => grant.CreatedBy == actor && grant.Account.DeletedOn == null && grant.Account.User.DeletedOn == null &&
                grant.Account.TenantId != null && tenants.Contains(grant.Account.TenantId.Value)).ToListAsync(cancellationToken);
        var userIds = grants.Select(grant => grant.Account.UserId).Distinct().ToArray();
        var deliveries = await database.WorkflowDeliveries.AsNoTracking().Where(deliveryRow => deliveryRow.ActorId == actor &&
            deliveryRow.Kind == "OPERATOR_ONBOARDING" && userIds.Contains(deliveryRow.UserId)).ToArrayAsync(cancellationToken);
        return grants.GroupBy(grant => grant.Account.UserId).Select(group =>
        {
            var user = group.First().Account.User;
            var delivery = deliveries.Where(deliveryRow => deliveryRow.UserId == user.Id).OrderByDescending(deliveryRow => deliveryRow.CreatedAt).FirstOrDefault();
            return new OperatorDto(user.Id!.Value, user.FullName, user.Email!, "operator", Status(user), actor,
                group.Select(grant => grant.Account.SiteId!.Value).ToArray(), group.SelectMany(grant => grant.Permissions).Distinct().ToArray(),
                delivery?.Id, delivery?.Status ?? "pending");
        }).OrderBy(operatorResult => operatorResult.FullName).ToArray();
    }
    private static string Status(User user) => user.Status == UserStatus.PendingVerification
        ? "pending_verification" : user.Status.ToString().ToLowerInvariant();

    public async Task<IReadOnlyList<OperatorAssignment>> AssignmentsAsync(Guid actor, CancellationToken cancellationToken) =>
        await database.OperatorGrants.AsNoTracking().Where(grant => grant.Account.UserId == actor &&
            grant.Account.User.Status == UserStatus.Active && grant.Account.User.DeletedOn == null &&
            grant.Account.Status == "ACTIVE" && grant.Account.DeletedOn == null && grant.Account.SiteId != null &&
            grant.Account.TenantId != null && grant.Account.AccountRoles.Any(r => r.RoleCode == "SITE_OPERATOR"))
            .Select(grant => new OperatorAssignment(grant.Account.SiteId!.Value, grant.Account.TenantId!.Value, grant.Permissions)).ToArrayAsync(cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        database.Users.AnyAsync(u => u.DeletedOn == null && u.Email != null && u.Email.ToLower() == email, cancellationToken);

    public void AddOperator(User user, IEnumerable<OperatorGrant> grants, AuthChallenge challenge, WorkflowDelivery delivery)
    {
        database.Users.Add(user);
        database.OperatorGrants.AddRange(grants);
        database.AuthChallenges.Add(challenge);
        database.WorkflowDeliveries.Add(delivery);
    }

    public async Task<(User User, AuthChallenge Challenge)?> LockInvitationAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = await database.AuthChallenges.AsNoTracking().Where(challengeRow => challengeRow.Id == id && challengeRow.Purpose == "BOOTSTRAP_PASSWORD")
            .Select(challengeRow => (Guid?)challengeRow.UserId).SingleOrDefaultAsync(cancellationToken);
        if (userId is null) return null;
        // All identity mutations lock the user before dependent challenge/account rows.
        var user = await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={userId.Value} FOR UPDATE")
            .Include(u => u.Accounts).ThenInclude(a => a.AccountRoles).SingleAsync(cancellationToken);
        var challenge = await database.AuthChallenges.FromSqlInterpolated($"SELECT * FROM auth_challenges WHERE id={id} FOR UPDATE").SingleAsync(cancellationToken);
        return (user, challenge);
    }
    public async Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken) =>
        await database.AuthSessions.Where(sessionRow => sessionRow.UserId == userId).ExecuteUpdateAsync(sessionRow => sessionRow.SetProperty(entity => entity.IsRevoked, true), cancellationToken);

    public async Task<User?> GetOperatorForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={userId} FOR UPDATE")
            .Include(user => user.Accounts).ThenInclude(account => account.AccountRoles).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<OperatorGrant>> GetOperatorGrantsAsync(Guid userId, CancellationToken cancellationToken) =>
        await database.OperatorGrants.AsNoTracking()
            .Where(grant => grant.Account.UserId == userId && grant.Account.DeletedOn == null)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<AuthChallenge>> GetPendingInvitationsAsync(Guid userId, CancellationToken cancellationToken) =>
        await database.AuthChallenges.AsNoTracking()
            .Where(challenge => challenge.UserId == userId && challenge.Purpose == "BOOTSTRAP_PASSWORD" && challenge.ConsumedAt == null)
            .ToArrayAsync(cancellationToken);

    public async Task ReplaceInvitationAsync(Guid userId, AuthChallenge challenge, WorkflowDelivery delivery, CancellationToken cancellationToken)
    {
        var now = delivery.CreatedAt;
        // User row held by the caller serializes activation and reissue.
        await database.AuthChallenges.Where(challengeRow => challengeRow.UserId == userId && challengeRow.Purpose == "BOOTSTRAP_PASSWORD" && challengeRow.ConsumedAt == null)
            .ExecuteUpdateAsync(sessionRow => sessionRow.SetProperty(challengeRow => challengeRow.ConsumedAt, now).SetProperty(challengeRow => challengeRow.CodeHash, ""), cancellationToken);
        await database.WorkflowDeliveries.Where(deliveryRow => deliveryRow.UserId == userId && deliveryRow.Kind == "OPERATOR_ONBOARDING" && deliveryRow.Status != "sent")
            .ExecuteUpdateAsync(sessionRow => sessionRow.SetProperty(deliveryRow => deliveryRow.Status, "cancelled").SetProperty(deliveryRow => deliveryRow.ProtectedPayload, ""), cancellationToken);
        database.AuthChallenges.Add(challenge);
        database.WorkflowDeliveries.Add(delivery);
    }
}
