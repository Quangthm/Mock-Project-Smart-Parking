using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(IUserManagementService users) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : throw AuthException.InvalidToken();
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> List([FromQuery] UserSearchDto query, CancellationToken ct) =>
        Ok(new { success = true, data = await users.ListAsync(ActorId, query, ct) });
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(new { success = true, data = await users.GetAsync(ActorId, id, ct) });
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UserStatusDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await users.SetStatusAsync(ActorId, id, body, ct) });

    [HttpPost]
    [Authorize(Roles = "owner")]
    public async Task<IActionResult> CreateOperator([FromBody] CreateOperatorDto body,
        [FromServices] IOperatorProvisioningService operators, CancellationToken ct)
    {
        var result = await operators.CreateAsync(ActorId, body, ct);
        return StatusCode(201, new { success = true, data = result });
    }
}
