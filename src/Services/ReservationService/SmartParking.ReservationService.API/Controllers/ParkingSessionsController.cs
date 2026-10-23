using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartParking.ReservationService.API.Controllers.Base;
using SmartParking.ReservationService.Application;

namespace SmartParking.ReservationService.API.Controllers;

[Route("api/parking-sessions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ParkingSessionsController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid siteId, CancellationToken ct)
    {
        var sessions = await Mediator.Send(new ListParkingSessionsQuery(siteId), ct);
        return Ok(new { success = true, data = sessions });
    }

    [HttpGet("walk-in/lookup")]
    public async Task<IActionResult> LookupWalkIn(
        [FromQuery] Guid siteId,
        [FromQuery] string plate,
        CancellationToken ct)
    {
        var session = await Mediator.Send(new FindWalkInSessionQuery(siteId, plate), ct);
        return Ok(new { success = true, data = session });
    }

    [HttpPost("walk-in/check-in")]
    public async Task<IActionResult> CheckInWalkIn(
        [FromQuery] Guid siteId,
        [FromBody] WalkInGateActionRequest body,
        CancellationToken ct)
    {
        var session = await Mediator.Send(
            new CheckInWalkInCommand(siteId, body.PlateNumber, body.ImageData, body.VehicleType), ct);
        return Ok(new { success = true, data = session });
    }

    [HttpPost("{sessionId:guid}/walk-in/check-out")]
    public async Task<IActionResult> CheckOutWalkIn(
        [FromRoute] Guid sessionId,
        [FromQuery] Guid siteId,
        [FromBody] WalkInGateActionRequest body,
        CancellationToken ct)
    {
        var session = await Mediator.Send(
            new CheckOutWalkInCommand(siteId, sessionId, body.PlateNumber, body.ImageData), ct);
        return Ok(new { success = true, data = session });
    }
}

public sealed record WalkInGateActionRequest(string PlateNumber, string? ImageData = null, string? VehicleType = null);
