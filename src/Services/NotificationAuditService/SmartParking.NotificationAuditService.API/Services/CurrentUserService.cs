using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Services;

namespace SmartParking.NotificationAuditService.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
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

            // Strictly allow header fallback ONLY in local Development environment
            if (_environment.IsDevelopment())
            {
                var headerUserId = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(headerUserId) && Guid.TryParse(headerUserId, out var fallbackParsed))
                {
                    return fallbackParsed;
                }
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

            // Strictly allow header fallback ONLY in local Development environment
            if (_environment.IsDevelopment())
            {
                var headerRole = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Role"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(headerRole))
                {
                    return headerRole;
                }
            }

            return null;
        }
    }

    public bool IsAuthenticated =>
        (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true && UserId.HasValue)
        || (_environment.IsDevelopment() && UserId.HasValue);
}
