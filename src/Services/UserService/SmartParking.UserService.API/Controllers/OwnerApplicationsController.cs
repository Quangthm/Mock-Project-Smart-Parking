using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Usecase.Management;

namespace UserService.API.Controllers;

[ApiController, Route("api/owner-applications"), Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerApplicationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new ListOwnerApplicationsQuery(), ct) });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var application = await mediator.Send(new GetOwnerApplicationQuery(id), ct);
        return application is null ? NotFound(new { success = false, code = "APPLICATION_NOT_FOUND" })
            : Ok(new { success = true, data = application });
    }

    [HttpPatch("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewOwnerDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new ReviewOwnerApplicationCommand(id, body), ct) });
}
