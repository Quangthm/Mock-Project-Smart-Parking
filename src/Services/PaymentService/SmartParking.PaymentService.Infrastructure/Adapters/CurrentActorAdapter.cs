using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using SmartParking.PaymentService.Application.Common.Interfaces;

namespace SmartParking.PaymentService.Infrastructure.Adapters;

public sealed class CurrentActorAdapter : ICurrentActor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostEnvironment? _environment;

    public CurrentActorAdapter(IHttpContextAccessor httpContextAccessor, IHostEnvironment? environment = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;
    private ClaimsPrincipal? User => Context?.User;
    private bool AllowDevHeaderFallback => _environment == null || _environment.IsDevelopment();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true || IsInternalService;

    public bool IsInternalService
    {
        get
        {
            if (Context == null) return false;

            if (AllowDevHeaderFallback &&
                Context.Request.Headers.TryGetValue("X-Internal-Service-Key", out var key) && !string.IsNullOrWhiteSpace(key))
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
            if (AllowDevHeaderFallback)
            {
                if (Context?.Request.Headers.TryGetValue("X-Actor-UserId", out var customUserId) == true &&
                    Guid.TryParse(customUserId, out var parsedCustomId))
                {
                    return parsedCustomId;
                }

                if (Context?.Request.Headers.TryGetValue("X-Actor-AccountId", out var customAccountId) == true &&
                    Guid.TryParse(customAccountId, out var parsedCustomAccountId))
                {
                    return parsedCustomAccountId;
                }
            }

            var sub = User?.FindFirstValue("account_id") ??
                      User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User?.FindFirstValue("sub");

            return Guid.TryParse(sub, out var parsed) ? parsed : null;
        }
    }

    public Guid? AccountId => UserId;

    public string? Role
    {
        get
        {
            if (AllowDevHeaderFallback &&
                Context?.Request.Headers.TryGetValue("X-Actor-Role", out var customRole) == true &&
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
            if (AllowDevHeaderFallback &&
                Context?.Request.Headers.TryGetValue("X-Actor-TenantId", out var customTenant) == true &&
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
