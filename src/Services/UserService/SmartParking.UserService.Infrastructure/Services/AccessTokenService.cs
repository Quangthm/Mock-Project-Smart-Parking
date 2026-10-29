using UserService.Application.Services;
using Microsoft.IdentityModel.Tokens;
using SmartParking.UserService.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Authorization;

namespace UserService.Infrastructure.Services;

public sealed class AccessTokenService(JwtOptions options, JwtKeyProvider keys,
    IAuthSessionStore sessions, TimeProvider clock) : IAccessTokenService
{
    public const int LifetimeSeconds = SessionLifetime.AccessSeconds;

    public static TokenValidationParameters ValidationParameters(JwtOptions options, JwtKeyProvider keys, TimeProvider? clock = null) => new()
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
        ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true,
        ValidIssuer = options.Issuer, ValidAudience = options.Audience,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256], IssuerSigningKey = keys.ValidationKey,
        ClockSkew = TimeSpan.Zero, NameClaimType = JwtRegisteredClaimNames.Sub, RoleClaimType = "role",
        LifetimeValidator = clock is null ? null : (notBefore, expires, _, _) =>
            expires.HasValue && expires.Value > clock.GetUtcNow().UtcDateTime &&
            (!notBefore.HasValue || notBefore.Value <= clock.GetUtcNow().UtcDateTime)
    };

    public static Account? SelectLoginAccount(User user) => user.Accounts
        .Where(a => a.Id is not null && a.Status == "ACTIVE" && a.DeletedOn == null && CurrentRole(a) is not null)
        .OrderBy(a => a.Id).FirstOrDefault();

    public static string? CurrentRole(User user) => SelectLoginAccount(user) is { } account ? CurrentRole(account) : null;

    public static string? CurrentRole(Account account) => AccessRoles.Map(account.RoleCode);

    public string GenerateAccessToken(User user, Account account, Guid accessTokenId, DateTimeOffset expiresAt)
    {
        if (account.UserId != user.Id || account.Id is null)
            throw new InvalidOperationException("The session account must belong to the authenticated User.");
        var role = CurrentRole(account) ?? throw new InvalidOperationException("A login account must have an assigned role.");
        var token = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id!.Value.ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, accessTokenId.ToString("N")),
             new Claim("account_id", account.Id.Value.ToString()), new Claim("role", role)],
            clock.GetUtcNow().UtcDateTime, expiresAt.UtcDateTime,
            new SigningCredentials(keys.SigningKey, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<bool> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return false;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(accessToken, ValidationParameters(options, keys, clock), out _);
            return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
                && Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var tokenId)
                && Guid.TryParse(principal.FindFirst("account_id")?.Value, out var accountId)
                && await sessions.IsActiveAsync(tokenId, userId, accountId, cancellationToken);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return false;
        }
    }
}
