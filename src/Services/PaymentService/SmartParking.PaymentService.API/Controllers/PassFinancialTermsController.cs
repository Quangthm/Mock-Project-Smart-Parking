using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Application.Usecase.PassPlans;

namespace SmartParking.PaymentService.API.Controllers;

[Authorize(Policy = UserJwtAuthentication.OwnerLotManagementPolicy)]
[ApiController]
[Route("api/v1/pricing/lots/{siteId:guid}/pass-plans/{planId:guid}/financial-terms")]
public sealed class PassFinancialTermsController(IMediator mediator) : ControllerBase
{
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpsertFinancialTerms(
        Guid siteId,
        Guid planId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        UpsertPassFinancialTermsRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpsertPassFinancialTermsCommand(siteId, planId, idempotencyKey, request),
            ct);

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
