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
        return await db.AuthSessions.Where(s => s.Id == previous.Id && s.UserId == previous.UserId
            && s.AccessTokenId == previous.AccessTokenId && s.TokenHash == previous.TokenHash
            && !s.IsRevoked && s.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.AccessTokenId, accessTokenId)
                .SetProperty(s => s.TokenHash, tokenHash)
                .SetProperty(s => s.AccessExpiresAt, accessExpiresAt), cancellationToken) == 1;
    }

    public async Task<bool> RevokeAsync(Guid accessTokenId, Guid userId, string tokenHash, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await db.AuthSessions.Where(s => s.AccessTokenId == accessTokenId && s.UserId == userId
            && s.TokenHash == tokenHash && !s.IsRevoked && s.ExpiresAt > now && s.AccessExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.IsRevoked, true), cancellationToken) == 1;
    }
}
