using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartParking.PaymentService.Application.Common.Interfaces;

namespace SmartParking.PaymentService.Infrastructure.Adapters;

public sealed class CurrentActorAdapter : ICurrentActor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentActorAdapter(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;
    private ClaimsPrincipal? User => Context?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true || IsInternalService;

    public bool IsInternalService
    {
        get
        {
            if (Context == null) return false;

            if (Context.Request.Headers.TryGetValue("X-Internal-Service-Key", out var key) && !string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            if (User?.HasClaim("scope", "s2s") == true ||
                User?.HasClaim(ClaimTypes.Role, "INTERNAL_SERVICE") == true)
            {
                return true;
            }

            return false;
        }
    }

    public Guid? UserId
    {
        get
        {
            if (Context?.Request.Headers.TryGetValue("X-Actor-UserId", out var customUserId) == true &&
                Guid.TryParse(customUserId, out var parsedCustomId))
            {
                return parsedCustomId;
            }

            var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User?.FindFirstValue("account_id") ??
                      User?.FindFirstValue("sub");

            return Guid.TryParse(sub, out var parsed) ? parsed : null;
        }
    }

    public string? Role
    {
        get
        {
            if (Context?.Request.Headers.TryGetValue("X-Actor-Role", out var customRole) == true &&
                !string.IsNullOrWhiteSpace(customRole))
            {
                return customRole.ToString().ToUpperInvariant();
            }

            if (IsInternalService) return "INTERNAL_SERVICE";

            return User?.FindFirstValue(ClaimTypes.Role) ??
                   User?.FindFirstValue("role");
        }
    }

    public Guid? TenantId
    {
        get
        {
            if (Context?.Request.Headers.TryGetValue("X-Actor-TenantId", out var customTenant) == true &&
                Guid.TryParse(customTenant, out var parsedCustomTenant))
            {
                return parsedCustomTenant;
            }

            var tenant = User?.FindFirstValue("tenant_id");
            return Guid.TryParse(tenant, out var parsed) ? parsed : null;
        }
    }

    public Guid? SiteId
    {
        get
        {
            if (Context?.Request.Headers.TryGetValue("X-Actor-SiteId", out var customSite) == true &&
                Guid.TryParse(customSite, out var parsedCustomSite))
            {
                return parsedCustomSite;
            }

            var site = User?.FindFirstValue("site_id");
            return Guid.TryParse(site, out var parsed) ? parsed : null;
        }
    }
}
