using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SmartParking.PaymentService.API.Middleware;

/// <summary>
/// Lightweight development authentication handler for isolated local testing (SPARK-229 / SPARK-321).
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
        // Support direct internal service key header
        if (Request.Headers.TryGetValue("X-Internal-Service-Key", out var internalKey) && !string.IsNullOrWhiteSpace(internalKey))
        {
            var internalClaims = new List<Claim>
            {
                new(ClaimTypes.Role, "INTERNAL_SERVICE"),
                new("scope", "s2s")
            };
            var s2sIdentity = new ClaimsIdentity(internalClaims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(s2sIdentity), Scheme.Name)));
        }

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

        var userId = "11111111-1111-1111-1111-111111111111";
        if (Request.Headers.TryGetValue("X-Actor-UserId", out var headerUserId) && !string.IsNullOrWhiteSpace(headerUserId))
        {
            userId = headerUserId.ToString();
        }

        var tenantId = "11111111-1111-1111-1111-111111111111";
        if (Request.Headers.TryGetValue("X-Actor-TenantId", out var headerTenantId) && !string.IsNullOrWhiteSpace(headerTenantId))
        {
            tenantId = headerTenantId.ToString();
        }

        var siteId = "8a123456-5717-4562-b3fc-2c963f66afa6";
        if (Request.Headers.TryGetValue("X-Actor-SiteId", out var headerSiteId) && !string.IsNullOrWhiteSpace(headerSiteId))
        {
            siteId = headerSiteId.ToString();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("account_id", userId),
            new("tenant_id", tenantId),
            new("site_id", siteId)
        };

        if (token.Contains("s2s", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new(ClaimTypes.Role, "INTERNAL_SERVICE"));
            claims.Add(new("scope", "s2s"));
        }
        else if (token.Contains("admin", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new(ClaimTypes.Role, "ADMIN"));
        }
        else if (token.Contains("operator", StringComparison.OrdinalIgnoreCase))
        {
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
