using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Persistence.Repositories;

public sealed class PostgresAuthSessionStore(AppDbContext db, TimeProvider clock) : IAuthSessionStore
{
    public async Task AddAsync(AuthSession session, CancellationToken cancellationToken)
    {
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsActiveAsync(Guid accessTokenId, Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return db.AuthSessions.AnyAsync(s => s.AccessTokenId == accessTokenId && s.UserId == userId
            && !s.IsRevoked && s.AccessExpiresAt > now && s.ExpiresAt > now, cancellationToken);
    }

    public Task<AuthSession?> FindByRefreshHashAsync(string hash, CancellationToken cancellationToken) =>
        db.AuthSessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

    public async Task<bool> RotateAsync(AuthSession previous, Guid accessTokenId, string tokenHash,
        DateTimeOffset accessExpiresAt, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        // Compare-and-swap: only one concurrent refresh may replace this token.
        var idleCutoff = now - SessionLifetime.RefreshInactivity;
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var updated = await db.AuthSessions.Where(s => s.Id == previous.Id && s.UserId == previous.UserId
            && s.AccessTokenId == previous.AccessTokenId && s.TokenHash == previous.TokenHash
            && !s.IsRevoked && s.ExpiresAt > now && (s.LastRefreshedAt ?? s.CreatedAt) > idleCutoff)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.AccessTokenId, accessTokenId)
                .SetProperty(s => s.TokenHash, tokenHash)
                .SetProperty(s => s.AccessExpiresAt, accessExpiresAt)
                .SetProperty(s => s.LastRefreshedAt, now), cancellationToken) == 1;
        if (!updated) return false;
        db.RotatedSessionProofs.Add(new() { AccessTokenId = previous.AccessTokenId, SessionId = previous.Id,
            UserId = previous.UserId, TokenHash = previous.TokenHash, AccessExpiresAt = previous.AccessExpiresAt });
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RevokeAsync(Guid accessTokenId, Guid userId, string tokenHash, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        // Resolve a stable session before writing. A rotation between authentication and
        // revocation cannot evade logout; historical proof is never accepted for login/refresh.
        var sessionId = await db.AuthSessions.Where(s => s.AccessTokenId == accessTokenId && s.UserId == userId
            && s.TokenHash == tokenHash && s.AccessExpiresAt > now).Select(s => (Guid?)s.Id).SingleOrDefaultAsync(cancellationToken)
            ?? await db.RotatedSessionProofs.Where(p => p.AccessTokenId == accessTokenId && p.UserId == userId &&
                p.TokenHash == tokenHash && p.AccessExpiresAt > now).Select(p => (Guid?)p.SessionId).SingleOrDefaultAsync(cancellationToken);
        if (sessionId is null) return false;
        return await db.AuthSessions.Where(s => s.Id == sessionId && s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.IsRevoked, true), cancellationToken) == 1;
    }
}
