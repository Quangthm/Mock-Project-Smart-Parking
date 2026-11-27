using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.FacilityService.Application.Discovery;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.API.Controllers;

[ApiController]
[Authorize(Policy = ReservationServiceAuthentication.DiscoveryCandidates)]
public sealed class DiscoverySlotCandidatesController(IMediator mediator) : ControllerBase
{
    public const string RoutePath = "/internal/discovery/v1/slot-candidates:read";

    [HttpPost(RoutePath)]
    [RequestSizeLimit(32 * 1024)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Read(ReadDiscoverySlotCandidatesQuery query, CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(query, ct), traceId = HttpContext.TraceIdentifier });
}
