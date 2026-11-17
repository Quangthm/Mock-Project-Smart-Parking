using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.Passes;

namespace SmartParking.OperationService.API.Controllers;

[Authorize(Roles = "driver")]
[Route("api/v1/pass-subscriptions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PassSubscriptionsController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpPost]
    public async Task<IActionResult> Prepare(PreparePassPurchaseCommand request, CancellationToken ct)
    {
        var result = await Mediator.Send(request, ct);
        return Ok(new { success = true, data = result });
    }
}
