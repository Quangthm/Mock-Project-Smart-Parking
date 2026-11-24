using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API.Controllers;

[Authorize(Roles = "driver")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReservationBookingController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpPost("/api/reservation-price-previews")]
    public async Task<IActionResult> Preview(ReservationPricePreview request, CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(new PreviewReservationPriceQuery(request), ct) });

    [HttpPost("/api/reservation-terms")]
    public async Task<IActionResult> Review(BookingSelection request, CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(new ReviewReservationTermsCommand(request), ct) });

    [HttpGet("/api/me/reservations/{id:guid}")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(new ReadOwnBookingQuery(id), ct) });

    [HttpGet("/api/me/reservations")]
    public async Task<IActionResult> List([FromQuery] int pageSize = 20, [FromQuery] DateTimeOffset? before = null,
        [FromQuery] Guid? beforeId = null, [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null, CancellationToken ct = default) =>
        Ok(new { success = true, data = await Mediator.Send(new ReadOwnBookingsQuery(pageSize, before, beforeId, from, to), ct) });
}
