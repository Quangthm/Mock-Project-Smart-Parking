using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using UserService.API.Controllers.Base;
using UserService.Application.DTOs;
using UserService.Application.Usecase.Login;

namespace UserService.API.Controllers;

[Route("api/auth")]
public class AuthController(
    IMediator mediator,
    IMapper mapper)
    : ApiControllerBase(mediator, mapper)
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto loginDto,
        CancellationToken cancellationToken)
    {
        var loginCommand = this.Mapper.Map<LoginCommand>(loginDto);

        var result = await this.Mediator.Send(
            loginCommand,
            cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Login successful",
            data = result
        });
    }
}