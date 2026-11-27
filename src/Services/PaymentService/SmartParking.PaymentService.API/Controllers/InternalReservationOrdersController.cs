using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.ReconcileReservation;
using SmartParking.PaymentService.Application.Usecase.Reservations.Queries.GetReservationCollections;

namespace SmartParking.PaymentService.API.Controllers;

[Authorize(Policy = OperationsServiceAuthentication.Policy)]
[ApiController]
[Route("internal")]
public sealed class InternalReservationOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalReservationOrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("reservation-deposit-orders/{orderId:guid}/collections")]
    [ProducesResponseType(typeof(CollectionLookupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCollections(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var query = new GetReservationCollectionsQuery(orderId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    [HttpPost("reservation-reconciliations")]
    [ProducesResponseType(typeof(ReconcileReservationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reconcile(
        [FromBody] ReconcileReservationRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new ReconcileReservationCollectionCommand(request);
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
