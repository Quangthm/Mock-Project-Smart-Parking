using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.PassPlans.Queries;

namespace SmartParking.OperationService.API.Controllers;

[AllowAnonymous]
[Route("api/v1/sites/{siteId:guid}/pass-offers")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SitePassOffersController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> GetOffers(
        Guid siteId,
        [FromQuery] string? vehicleType,
        CancellationToken ct)
    {
        var result = await Mediator.Send(new GetSitePassOffersQuery(siteId, vehicleType), ct);
        return Ok(new { success = true, data = result });
    }
}
