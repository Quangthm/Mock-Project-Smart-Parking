using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Application.Usecase.PassPlans;

namespace SmartParking.PaymentService.API.Controllers;

[Authorize(Policy = OperationsServiceAuthentication.PassClosuresCompensatePolicy)]
[ApiController]
[Route("internal/pass-closures")]
public sealed class InternalPassClosuresController(IMediator mediator) : ControllerBase
{
    [HttpPost("compensate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Compensate(
        CompensatePassClosureRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new CompensatePassClosureCommand(request), ct);
        if (result.IsReplayed)
        {
            Response.Headers.TryAdd("Idempotency-Replayed", "true");
        }

        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
