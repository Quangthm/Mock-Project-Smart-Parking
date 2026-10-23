using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartParking.ReservationService.API.Controllers.Base;
using SmartParking.ReservationService.Application;

namespace SmartParking.ReservationService.API.Controllers;

[Route("api/reservations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReservationsController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid siteId, CancellationToken ct)
    {
        var items = await Mediator.Send(new ListReservationsQuery(siteId), ct);
        return Ok(new { success = true, data = items });
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup(
        [FromQuery] Guid siteId,
        [FromQuery] string query,
        CancellationToken ct)
    {
        var entry = await Mediator.Send(new FindReservationQuery(siteId, query), ct);
        return Ok(new { success = true, data = entry });
    }

    [HttpPost("{reservationId:guid}/check-in")]
    public async Task<IActionResult> CheckIn(
        [FromRoute] Guid reservationId,
        [FromQuery] Guid siteId,
        [FromBody] GateActionRequest body,
        CancellationToken ct)
    {
        var entry = await Mediator.Send(
            new CheckInReservationCommand(siteId, reservationId, body.PlateNumber, body.ImageData), ct);
        return Ok(new { success = true, data = entry });
    }

    [HttpPost("{reservationId:guid}/check-out")]
    public async Task<IActionResult> CheckOut(
        [FromRoute] Guid reservationId,
        [FromQuery] Guid siteId,
        [FromBody] GateActionRequest body,
        CancellationToken ct)
    {
        var entry = await Mediator.Send(
            new CheckOutReservationCommand(siteId, reservationId, body.PlateNumber, body.ImageData), ct);
        return Ok(new { success = true, data = entry });
    }
}

public sealed record GateActionRequest(string? PlateNumber, string? ImageData = null);
