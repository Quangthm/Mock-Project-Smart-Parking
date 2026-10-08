using Microsoft.IdentityModel.Tokens;
using SmartParking.UserService.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;

namespace UserService.Application.Services;

public sealed class AccessTokenService(JwtOptions options, JwtKeyProvider keys,
    IAuthSessionStore sessions, TimeProvider clock) : IAccessTokenService
{
    public const int LifetimeSeconds = 24 * 60 * 60;

    public static TokenValidationParameters ValidationParameters(JwtOptions options, JwtKeyProvider keys) => new()
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
        ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true,
        ValidIssuer = options.Issuer, ValidAudience = options.Audience,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256], IssuerSigningKey = keys.ValidationKey,
        ClockSkew = TimeSpan.Zero, NameClaimType = JwtRegisteredClaimNames.Sub, RoleClaimType = "role"
    };

    public static string? CurrentRole(User user)
    {
        var code = user.Accounts.FirstOrDefault(a => a.Status == "ACTIVE" && a.DeletedOn == null && a.AccountRoles.Any())?
            .AccountRoles.First().RoleCode;
        return code?.ToUpperInvariant() switch
        {
            "PLATFORM_ADMIN" or "ADMIN" => "admin",
            "BUSINESS_OWNER" or "OWNER" => "owner",
            "SITE_OPERATOR" or "OPERATOR" => "operator",
            "DRIVER" => "driver",
            _ => null
        };
    }

    public string GenerateAccessToken(User user, Guid accessTokenId, DateTimeOffset expiresAt)
    {
        var role = CurrentRole(user) ?? throw new InvalidOperationException("A login account must have an assigned role.");
        var token = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id!.Value.ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, accessTokenId.ToString("N")), new Claim("role", role)],
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
            var principal = handler.ValidateToken(accessToken, ValidationParameters(options, keys), out _);
            return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
                && Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var tokenId)
                && await sessions.IsActiveAsync(tokenId, userId, cancellationToken);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return false;
        }
    }
}
