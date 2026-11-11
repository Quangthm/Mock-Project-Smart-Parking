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
    public async Task<IActionResult> Create(CreateReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var receipt = await Mediator.Send(new AdmitReservationCommand(request, idempotencyKey), cancellationToken);
        return AcceptedAtAction(nameof(Read), new { requestId = receipt.RequestId }, new { success = true, data = receipt });
    }

    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> Read(Guid requestId, CancellationToken cancellationToken)
    {
        var receipt = await Mediator.Send(new ReadReservationReceiptQuery(requestId), cancellationToken);
        return Ok(new { success = true, data = receipt });
    }
}
