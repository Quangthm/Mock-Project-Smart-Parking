using System.IdentityModel.Tokens.Jwt;
using System.Diagnostics;
using System.Security.Claims;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.API.Services;

/// <summary>
/// Reads the authenticated subject from the current HTTP request.
/// </summary>
/// <remarks>
/// The subject comes from the validated token, never from the request body or a header the
/// caller controls. The role claim is carried for convenience only: the Application layer
/// re-checks the current role against the authoritative account record.
/// </remarks>
public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal
        ? principal : null;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public Guid? AccountId =>
        Guid.TryParse(Principal?.FindFirst("account_id")?.Value, out var id) ? id : null;

    public Guid? AccessTokenId =>
        Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var id) ? id : null;

    public string? Role => Principal?.FindFirst("role")?.Value;
    public string CorrelationId => Activity.Current?.TraceId.ToString()
        ?? accessor.HttpContext?.TraceIdentifier ?? string.Empty;
}
