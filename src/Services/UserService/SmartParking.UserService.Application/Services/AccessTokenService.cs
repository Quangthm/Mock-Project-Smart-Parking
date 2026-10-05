using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.UserService.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;

namespace UserService.Application.Services;

public sealed class AccessTokenService(
    IConfiguration configuration, IAuthSessionStore sessions, TimeProvider clock) : IAccessTokenService
{
    public const int LifetimeSeconds = 300; // Existing demo lifetime; see the decision report.

    public static TokenValidationParameters ValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
        ClockSkew = TimeSpan.Zero,
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = "role"
    };

    public string GenerateAccessToken(User user)
    {
        var options = configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");
        var sessionId = Guid.NewGuid().ToString("N");
        var now = clock.GetUtcNow();
        var expires = now.AddSeconds(LifetimeSeconds);
        var role = user.Accounts.FirstOrDefault(a => a.Status == "ACTIVE")?.AccountRoles.FirstOrDefault()?.RoleCode.ToLowerInvariant()
            ?? throw new InvalidOperationException("A login account must have an assigned role.");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id!.Value.ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, sessionId), new Claim("role", role)],
            now.UtcDateTime, expires.UtcDateTime, credentials);
        var encodedToken = new JwtSecurityTokenHandler().WriteToken(token);
        sessions.Add(sessionId, user.Id.Value, expires);
        return encodedToken;
    }

    public bool ValidateAccessToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return false;
        try
        {
            var options = configuration.GetSection("Jwt").Get<JwtOptions>();
            if (options is null) return false;
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(accessToken, ValidationParameters(options), out _);
            return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
                && sessions.IsActive(principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? "", userId);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return false;
        }
    }

    public string RefreshAccessToken(string accessToken, string refreshToken) =>
        throw new NotSupportedException("Refresh is not implemented in this demo. Sign in again.");
}
