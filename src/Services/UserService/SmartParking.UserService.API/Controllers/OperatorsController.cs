using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Authorization;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Usecase.Management;

namespace UserService.API.Controllers;

[ApiController, Route("api/operators"), Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OperatorsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new ListOperatorsQuery(), ct) });

    [HttpGet("me")]
    public async Task<IActionResult> Assignments(CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new GetOperatorAssignmentsQuery(), ct) });

    [HttpGet("slot-scope")]
    public async Task<IActionResult> SlotScope(Guid siteId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetOperatorScopeQuery(siteId, OperatorScopeAction.SlotOverride), ct));

    [HttpGet("site-scope")]
    public async Task<IActionResult> SiteScope(Guid siteId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetOperatorScopeQuery(siteId, OperatorScopeAction.AssignedSite), ct));
}
