using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Application.Usecase.PassPlans;

namespace SmartParking.PaymentService.API.Controllers;

[Authorize(Policy = UserJwtAuthentication.OwnerLotManagementPolicy)]
[ApiController]
[Route("api/v1/payments/owners/lots/{siteId:guid}/pass-revenue-summary")]
public sealed class PassRevenueController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPassRevenueSummary(
        Guid siteId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? productType,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetPassRevenueSummaryQuery(siteId, from, to, productType),
            ct);

        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
