using System.Security.Claims;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Services;

namespace SmartParking.NotificationAuditService.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value
                              ?? user.FindFirst("uid")?.Value;

                if (Guid.TryParse(idClaim, out var parsed))
                {
                    return parsed;
                }
            }

            // Dev / testing fallback header
            var headerUserId = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerUserId) && Guid.TryParse(headerUserId, out var fallbackParsed))
            {
                return fallbackParsed;
            }

            return null;
        }
    }

    public string? UserRole
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value
                                ?? user.FindFirst("role")?.Value;

                if (!string.IsNullOrWhiteSpace(roleClaim))
                {
                    return roleClaim;
                }
            }

            // Dev / testing fallback header
            var headerRole = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Role"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerRole))
            {
                return headerRole;
            }

            return null;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;
}
