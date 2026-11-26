using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Application.Usecase.PassPlans;

namespace SmartParking.PaymentService.API.Controllers;

[ApiController]
[Route("internal/pass-financial-terms")]
public sealed class InternalPassFinancialTermsController(IMediator mediator) : ControllerBase
{
    [Authorize(Policy = OperationsServiceAuthentication.PassFinancialTermsReadPolicy)]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTerms(
        [FromQuery] Guid siteId,
        [FromQuery] Guid? planId,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetInternalPassFinancialTermsQuery(siteId, planId), ct);
        return Ok(new
        {
            success = true,
            data = result
        });
    }

    [Authorize(Policy = OperationsServiceAuthentication.PassFinancialTermsQuotePolicy)]
    [HttpPost("quote-snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> QuoteSnapshot(
        QuotePassFinancialTermsRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(new QuotePassFinancialTermsCommand(request), ct);
        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
