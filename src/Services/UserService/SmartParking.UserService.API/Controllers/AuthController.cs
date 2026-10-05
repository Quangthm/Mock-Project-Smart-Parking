using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using UserService.API.Controllers.Base;
using UserService.Application.DTOs;
using UserService.Application.Usecase.Login;
using UserService.Application.Usecase.Session;

namespace UserService.API.Controllers;

[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController(IMediator mediator, IMapper mapper) : ApiControllerBase(mediator, mapper)
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(Mapper.Map<LoginCommand>(loginDto), cancellationToken);
        if (!result.IsSuccess)
            return Unauthorized(new { success = false, message = "Invalid email or password." });
        return Ok(new { success = true, message = "Login successful", data = result.Session });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            return Unauthorized();
        var user = await Mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);
        return user is null ? Unauthorized() : Ok(new { success = true, data = user });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var sessionId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrEmpty(sessionId)) return Unauthorized();
        await Mediator.Send(new LogoutCommand(sessionId), cancellationToken);
        return NoContent();
    }
}
