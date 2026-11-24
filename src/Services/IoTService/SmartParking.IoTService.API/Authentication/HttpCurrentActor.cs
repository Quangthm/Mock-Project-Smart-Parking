using SmartParking.IoTService.Application.Assistant.Authorization;

namespace SmartParking.IoTService.API.Authentication;

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    internal static readonly object ItemKey = new();
    public CurrentActor? Actor => accessor.HttpContext?.User.Identity?.IsAuthenticated == true
        ? accessor.HttpContext.Items[ItemKey] as CurrentActor : null;
}
