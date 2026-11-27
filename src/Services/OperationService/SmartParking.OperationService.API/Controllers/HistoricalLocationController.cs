using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.Application.Discovery;

namespace SmartParking.OperationService.API.Controllers;

[ApiController]
[Authorize(Roles = "driver")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HistoricalLocationController(IMediator mediator) : ControllerBase
{
    [HttpGet("/api/me/discovery/last-booked-location")]
    public async Task<IActionResult> Read(CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new ReadHistoricalLocationQuery(), ct),
            traceId = HttpContext.TraceIdentifier });
}
