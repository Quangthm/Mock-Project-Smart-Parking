using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Authorization;

namespace UserService.Persistence.Repositories;

public sealed class AccountAccessDecisionRepository(AppDbContext db) : IAccountAccessDecisionRepository
{
    public async Task LockWorkflowAsync(Guid workflowId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Decision replay requires a UoW transaction.");
        var resource = $"account-current-workflow:{workflowId:D}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0))", ct);
    }
    public Task<AccountAccessDecision?> FindAsync(Guid workflowId, CancellationToken ct) =>
        db.Set<AccountAccessDecision>().AsNoTracking().SingleOrDefaultAsync(x => x.WorkflowId == workflowId, ct);
    public void Add(AccountAccessDecision decision) => db.Set<AccountAccessDecision>().Add(decision);
}

public sealed class AccountAuthorityRepository(AppDbContext db) : IAccountAuthorityRepository
{
    public Task<CurrentAccountAuthority?> ReadAsync(Guid userId, Guid accountId, DateTimeOffset now, CancellationToken ct) =>
        db.Accounts.AsNoTracking().Where(a => a.Id == accountId && a.UserId == userId)
            .Select(a => new CurrentAccountAuthority(
                a.User.DeletedOn == null && a.User.Status == UserStatus.Active && (a.User.LockedUntil == null || a.User.LockedUntil <= now),
                a.DeletedOn == null && a.Status == "ACTIVE",
                a.RoleCode == "DRIVER" && db.Roles.Any(r => r.Code == a.RoleCode), a.Permissions))
            .SingleOrDefaultAsync(ct);
}
