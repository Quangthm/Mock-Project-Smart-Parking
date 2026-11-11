using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.Usecase.Checkout;
using SmartParking.OperationService.Application.Usecase.ReservationGate;

namespace SmartParking.OperationService.API.Controllers;

[Route("api/checkout")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CheckoutController(IMediator mediator, IHostEnvironment environment) : ApiControllerBase(mediator)
{
    [HttpPost("pricing")]
    public async Task<IActionResult> Pricing([FromBody] PricingRequest body, CancellationToken ct) =>
        Ok(new
        {
            success = true,
            data = await Mediator.Send(new CalculateCheckoutPricingQuery(
                body.SiteId, body.ReferenceId, body.ReferenceType, body.VehicleType,
                body.EntryTime, body.ExitTime), ct)
        });

    [HttpPost("reservations/{reservationId:guid}/prepare")]
    public async Task<IActionResult> PrepareReservation(
        Guid reservationId,
        [FromQuery] Guid siteId,
        [FromBody] CheckoutRequest body,
        CancellationToken ct) =>
        Ok(new
        {
            success = true,
            data = await Mediator.Send(
                new PrepareReservationCheckoutCommand(siteId, reservationId, body.PlateNumber), ct)
        });

    [HttpPost("sessions/{sessionId:guid}/prepare")]
    public async Task<IActionResult> PrepareSession(
        Guid sessionId,
        [FromQuery] Guid siteId,
        [FromBody] CheckoutRequest body,
        CancellationToken ct) =>
        Ok(new
        {
            success = true,
            data = await Mediator.Send(
                new PrepareSessionCheckoutCommand(siteId, sessionId, body.PlateNumber), ct)
        });

    [HttpPost("sessions/{sessionId:guid}/prepare-test-payos")]
    public async Task<IActionResult> PrepareSessionTestPayOs(
        Guid sessionId,
        [FromQuery] Guid siteId,
        [FromBody] CheckoutRequest body,
        CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        return Ok(new
        {
            success = true,
            data = await Mediator.Send(
                new PrepareSessionCheckoutCommand(siteId, sessionId, body.PlateNumber, 2000m), ct)
        });
    }

    [HttpPost("orders/{orderId:guid}/payos")]
    public async Task<IActionResult> PayOs(Guid orderId, [FromBody] IdempotencyRequest body, CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(
            new InitiateCheckoutCommand(orderId, body.IdempotencyKey), ct) });

    [HttpPost("orders/{orderId:guid}/cash")]
    public async Task<IActionResult> Cash(Guid orderId, [FromBody] IdempotencyRequest body, CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(
            new ConfirmCashCheckoutCommand(orderId, body.IdempotencyKey), ct) });

    [HttpPost("orders/{orderId:guid}/demo-pay")]
    public async Task<IActionResult> DemoPay(Guid orderId, CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(new DemoPayCheckoutCommand(orderId), ct) });

    [HttpGet("transactions/{transactionId:guid}/status")]
    public async Task<IActionResult> Status(Guid transactionId, CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(new GetCheckoutStatusQuery(transactionId), ct) });

    public sealed record CheckoutRequest(string? PlateNumber);
    public sealed record PricingRequest(
        Guid SiteId,
        Guid ReferenceId,
        string ReferenceType,
        string VehicleType,
        DateTimeOffset EntryTime,
        DateTimeOffset ExitTime);
    public sealed record IdempotencyRequest(string IdempotencyKey);
}
