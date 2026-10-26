using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Persistence.Repositories;

/// <summary>
/// Coordinates profile saves with updates/deletes of the authoritative authorization rows.
/// Lock order is User, Account, AccountRole (role code), then the exact session.
/// </summary>
public sealed class ProfileAccessStore(AppDbContext db) : IProfileAccessStore
{
    public async Task<ProfileAccessState?> LoadCurrentAsync(Guid accountId, Guid accessTokenId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == account.UserId, cancellationToken);
        var session = await db.AuthSessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.AccessTokenId == accessTokenId, cancellationToken);
        if (user is null || session is null) return null;
        var roles = await db.AccountRoles.AsNoTracking().Where(r => r.AccountId == accountId)
            .OrderBy(r => r.RoleCode).Select(r => r.RoleCode).ToArrayAsync(cancellationToken);
        return new(user, account, session, roles);
    }

    public async Task<ProfileAccessState?> LockCurrentAsync(Guid accountId, Guid accessTokenId, CancellationToken cancellationToken)
    {
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Profile authorization locks require a unit-of-work transaction.");
        if (transaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Profile authorization requires ReadCommitted isolation.");

        try
        {
            // Bound contention, without replaying a transaction whose commit outcome could be unknown.
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);

            // This is only a lookup hint. Account ownership is read again after both row locks.
            var userId = await db.Accounts.AsNoTracking().Where(a => a.Id == accountId)
                .Select(a => (Guid?)a.UserId).SingleOrDefaultAsync(cancellationToken);
            if (userId is null) return null;
            var freshUser = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {userId.Value} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (freshUser is null) return null;
            var account = await db.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {accountId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (account is null || account.UserId != userId.Value) return null;
            var roles = await db.AccountRoles.FromSqlInterpolated(
                    $"SELECT * FROM account_roles WHERE account_id = {accountId} ORDER BY role_code FOR UPDATE")
                .AsNoTracking().ToArrayAsync(cancellationToken);
            var session = await db.AuthSessions.FromSqlInterpolated(
                    $"SELECT * FROM user_refresh_tokens WHERE access_token_id = {accessTokenId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (session is null) return null;

            // Authentication may already have tracked this User. A locking tracking query alone
            // would return that stale instance. Reload only this entity, preserving unrelated work.
            var tracked = db.ChangeTracker.Entries<User>().SingleOrDefault(e => e.Entity.Id == userId.Value);
            User user;
            if (tracked is null)
            {
                user = freshUser;
                db.Users.Attach(user);
            }
            else
            {
                if (tracked.State != EntityState.Unchanged)
                    throw new InvalidOperationException("The profile User must have no pending mutations before authorization.");
                await tracked.ReloadAsync(cancellationToken);
                user = tracked.Entity;
            }

            // Detached Account/Session/role values are intentional: policy must not inspect an
            // earlier tracked navigation graph that relationship fixup could reintroduce.
            return new(user, account, session, roles.Select(r => r.RoleCode).ToArray());
        }
        catch (PostgresException exception) when (exception.SqlState is
            PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure)
        {
            throw new ProfileAccessConflictException(exception);
        }
    }
}
