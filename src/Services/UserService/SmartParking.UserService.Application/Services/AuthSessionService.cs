using SmartParking.UserService.Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Models.JwT;
using UserService.Application.DTOs;

namespace UserService.Application.Services;

public sealed class AuthSessionService(IAccessTokenService tokens, IAuthSessionStore sessions,
    AuthenticationPolicy policy, TimeProvider clock)
{
    public static string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public async Task<UserSessionDto> CreateAsync(User user, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var refresh = NewRefreshToken();
        var session = new AuthSession
        {
            Id = Guid.NewGuid(), UserId = user.Id!.Value, AccessTokenId = Guid.NewGuid(),
            TokenHash = HashRefreshToken(refresh), AccessExpiresAt = now.AddSeconds(AccessTokenService.LifetimeSeconds),
            ExpiresAt = now.AddDays(policy.RefreshTokenDays), CreatedAt = now
        };
        var result = Response(user, session.AccessTokenId, refresh, session.AccessExpiresAt);
        await sessions.AddAsync(session, cancellationToken);
        return result;
    }

    public async Task<UserSessionDto> RotateAsync(User user, AuthSession previous, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (previous.UserId != user.Id || previous.IsRevoked || previous.ExpiresAt <= now)
            throw AuthException.InvalidToken();
        var id = Guid.NewGuid();
        var refresh = NewRefreshToken();
        var expires = now.AddSeconds(AccessTokenService.LifetimeSeconds);
        if (expires > previous.ExpiresAt) expires = previous.ExpiresAt;
        var result = Response(user, id, refresh, expires);
        if (!await sessions.RotateAsync(previous, id, HashRefreshToken(refresh), expires, cancellationToken))
            throw AuthException.InvalidToken();
        return result;
    }

    private UserSessionDto Response(User user, Guid id, string refresh, DateTimeOffset expires) => new()
    {
        AccessToken = tokens.GenerateAccessToken(user, id, expires), RefreshToken = refresh,
        ExpiresIn = (int)Math.Ceiling((expires - clock.GetUtcNow()).TotalSeconds),
        User = new UserInfoDto
        {
            UserId = user.Id!.Value, FullName = user.FullName, Email = user.Email ?? string.Empty,
            Role = AccessTokenService.CurrentRole(user)!
        }
    };
}
