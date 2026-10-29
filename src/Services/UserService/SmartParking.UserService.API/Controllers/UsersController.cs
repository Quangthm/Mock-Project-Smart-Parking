using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using MediatR;
using UserService.Application.Usecase.IdentityAccess;

namespace UserService.API.Controllers;

[ApiController, Route("api/users"), Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] UserSearchDto query, CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new ListUsersQuery(query), ct) });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new GetManagedUserQuery(id), ct) });

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UserStatusDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new SetManagedUserStatusCommand(id, body), ct) });

    [HttpPost]
    [Authorize(Roles = "owner")]
    public async Task<IActionResult> CreateOperator([FromBody] CreateOperatorDto body,
        [FromServices] ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new CreateOperatorCommand(ActorId, body), ct);
        return StatusCode(201, new { success = true, data = result });
    }
}
