using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Authentication;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.API.Controllers;

[Authorize(Policy = PassPlanServiceAuthentication.Policy)]
[Route("internal/pass-plans")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InternalPassPlansController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpPost("validate-and-freeze")]
    public async Task<IActionResult> ValidateAndFreeze(
        ValidateAndFreezePassPlanRequest request,
        CancellationToken ct)
    {
        var result = await Mediator.Send(new ValidateAndFreezePassPlanCommand(request), ct);
        return Ok(result);
    }
}
