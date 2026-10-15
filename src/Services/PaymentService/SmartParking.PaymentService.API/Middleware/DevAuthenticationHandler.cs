using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SmartParking.PaymentService.API.Middleware;

/// <summary>
/// Lightweight development authentication handler for isolated local testing (SPARK-229).
/// Simulates API Gateway identity forwarding without requiring UserService or external identity providers.
/// Only activated when ASPNETCORE_ENVIRONMENT == "Development".
/// </summary>
public sealed class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevBearer";

    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authHeader.ToString()["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Empty bearer token."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"),
            new("tenant_id", "11111111-1111-1111-1111-111111111111")
        };

        if (token.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
            token.Contains("operator", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new(ClaimTypes.Role, "ADMIN"));
            claims.Add(new(ClaimTypes.Role, "OPERATOR"));
        }
        else
        {
            claims.Add(new(ClaimTypes.Role, "DRIVER"));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
