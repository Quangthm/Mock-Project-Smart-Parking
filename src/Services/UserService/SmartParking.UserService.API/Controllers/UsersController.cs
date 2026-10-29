using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Usecase.Management;

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
    public async Task<IActionResult> CreateOperator([FromBody] CreateOperatorDto body, CancellationToken ct) =>
        StatusCode(201, new { success = true, data = await mediator.Send(new CreateOperatorCommand(body), ct) });
}
