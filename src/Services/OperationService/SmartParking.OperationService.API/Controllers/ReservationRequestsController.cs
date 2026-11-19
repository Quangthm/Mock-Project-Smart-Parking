using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API.Controllers;

[Authorize(Roles = "driver")]
[Route("api/reservation-requests")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReservationRequestsController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JsonElement body,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        [FromServices] IOptions<JsonOptions> options, CancellationToken cancellationToken)
    {
        if (body.ValueKind != JsonValueKind.Object)
            throw new ReservationGateException(400, "INVALID_REQUEST", "Expected a reservation selection.");
        var quote = body.EnumerateObject().Any(field =>
            field.Name.Equals("licensePlate", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Equals("quoteId", StringComparison.OrdinalIgnoreCase) ||
            field.Name.Equals("quoteVersion", StringComparison.OrdinalIgnoreCase));
        IRequest<ReservationReceipt> command;
        try
        {
            var serializer = options.Value.JsonSerializerOptions;
            command = quote
                ? new AdmitQuoteReservation(body.Deserialize<QuoteReservationIntent>(serializer)!, idempotencyKey)
                : new AdmitReservationCommand(body.Deserialize<CreateReservationRequest>(serializer)!, idempotencyKey);
        }
        catch (JsonException)
        {
            throw new ReservationGateException(400, "INVALID_REQUEST", "Invalid reservation selection.");
        }
        var receipt = await Mediator.Send(command, cancellationToken);
        return AcceptedAtAction(nameof(Read), new { requestId = receipt.RequestId }, new { success = true, data = receipt });
    }

    [HttpGet("{requestId:guid}/progress")]
    public async Task<IActionResult> Progress(Guid requestId, CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(new ReadOwnReservationProgress(requestId), ct) });

    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> Read(Guid requestId, CancellationToken cancellationToken)
    {
        var receipt = await Mediator.Send(new ReadReservationReceiptQuery(requestId), cancellationToken);
        return Ok(new { success = true, data = receipt });
    }
}
