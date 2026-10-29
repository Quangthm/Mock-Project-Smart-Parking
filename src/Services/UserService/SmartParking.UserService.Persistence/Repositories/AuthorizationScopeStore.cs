using Microsoft.EntityFrameworkCore;
using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Persistence.Repositories;

public sealed class AuthorizationScopeStore(AppDbContext db) : IAuthorizationScopeStore
{
    public async Task<IReadOnlyList<AccountMembership>> LoadAsync(Guid userId, bool lockRows, CancellationToken ct)
    {
        if (lockRows && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Membership locks require the use case transaction.");
        // The caller first locks the bound User through ICurrentAccessService. Sibling
        // memberships then use a stable order, with no business-policy predicates in SQL.
        var accounts = lockRows
            ? await db.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE user_id = {userId} ORDER BY id FOR SHARE")
                .AsNoTracking().ToArrayAsync(ct)
            : await db.Accounts.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.Id).ToArrayAsync(ct);
        var accountIds = accounts.Select(a => a.Id!.Value).ToArray();
        var grants = await db.OperatorGrants.AsNoTracking().Where(g => accountIds.Contains(g.AccountId)).ToArrayAsync(ct);
        return accounts.Select(a => new AccountMembership(a.Id!.Value, a.UserId, a.RoleCode, a.Status,
            a.DeletedOn, a.TenantId, a.SiteId, a.Permissions,
            grants.SingleOrDefault(g => g.AccountId == a.Id)?.Permissions)).ToArray();
    }
}
