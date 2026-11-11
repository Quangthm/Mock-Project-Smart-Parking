using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API;

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public Guid? UserId => Read("sub");
    public Guid? AccountId => Read("account_id");
    public string? Role => Principal?.Identity?.IsAuthenticated == true ? Principal.FindFirst("role")?.Value : null;
    private Guid? Read(string type) => Principal?.Identity?.IsAuthenticated == true &&
        Guid.TryParse(Principal.FindFirst(type)?.Value, out var id) && id != Guid.Empty ? id : null;
}
