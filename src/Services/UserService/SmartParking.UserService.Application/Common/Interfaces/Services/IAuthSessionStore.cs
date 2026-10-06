using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Services;

public interface IAuthSessionStore
{
    Task AddAsync(AuthSession session, CancellationToken cancellationToken);
    Task<bool> IsActiveAsync(Guid accessTokenId, Guid userId, CancellationToken cancellationToken);
    Task<AuthSession?> FindByRefreshHashAsync(string hash, CancellationToken cancellationToken);
    Task<bool> RotateAsync(AuthSession previous, Guid accessTokenId, string tokenHash,
        DateTimeOffset accessExpiresAt, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(Guid accessTokenId, Guid userId, string tokenHash, CancellationToken cancellationToken);
}
