using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.FacilityService.Application.Discovery;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.API.Controllers;

[ApiController]
[Authorize(Policy = ReservationServiceAuthentication.DiscoverySiteLocation)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublishedSiteLocationController(IMediator mediator) : ControllerBase
{
    public const string RoutePath = "/internal/discovery/v1/sites/{siteId:guid}/location";

    [HttpGet(RoutePath)]
    public async Task<IActionResult> Read(Guid siteId, CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new ReadPublishedSiteLocationQuery(siteId), ct),
            traceId = HttpContext.TraceIdentifier });
}
