using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.IdentityModel.Tokens.Jwt;
using UserService.API.Controllers.Base;
using UserService.Application.DTOs;
using UserService.Application.Usecase.Login;
using UserService.Application.Usecase.Session;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.API.Controllers;

[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController(IMediator mediator, IMapper mapper) : ApiControllerBase(mediator, mapper)
{
    [AllowAnonymous]
    [HttpPost("register/owner")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> RegisterOwner([FromBody] OwnerRegistrationDto body,
        [FromServices] IOwnerRegistrationService registrations, CancellationToken ct) =>
        StatusCode(201, new { success = true, data = await registrations.RegisterAsync(body, ct) });

    [AllowAnonymous]
    [HttpPost("register/driver")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> RegisterDriver([FromBody] DriverRegistrationDto body,
        [FromServices] IDriverRegistrationService registrations, CancellationToken ct) =>
        StatusCode(201, new { success = true, data = await registrations.RegisterAsync(body, ct) });
    [AllowAnonymous,HttpPost("register/driver/recover"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> RecoverDriver(OtpLoginDto body,[FromServices] IDriverRegistrationService registrations,CancellationToken ct)=>
        Ok(new{success=true,data=await registrations.RecoverAsync(body.Contact,ct)});

    [AllowAnonymous]
    [HttpPost("register/driver/verify")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> VerifyDriver([FromBody] VerifyDriverOtpDto body,
        [FromServices] IDriverRegistrationService registrations, CancellationToken ct)
    {
        await registrations.VerifyAsync(body, ct);
        return Ok(new { success = true, message = "Account verified. You can now sign in." });
    }

    [AllowAnonymous]
    [HttpPost("register/driver/resend")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> ResendDriver([FromBody] ResendDriverOtpDto body,
        [FromServices] IDriverRegistrationService registrations, CancellationToken ct) =>
        Ok(new { success = true, data = await registrations.ResendAsync(body.RegistrationId, ct) });

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(Mapper.Map<LoginCommand>(loginDto), cancellationToken);
        if (!result.IsSuccess)
            return StatusCode(result.ErrorCode == "ACCOUNT_LOCKED" ? 403 : 401, new
            {
                success = false, code = result.ErrorCode,
                message = result.ErrorCode == "ACCOUNT_LOCKED" ? "Account is locked." : result.ErrorCode=="MFA_REQUIRED"?"Use OTP sign-in with your authenticator code.":"Invalid email or password."
            });
        return Ok(new { success = true, message = "Login successful", data = result.Session });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            return InvalidToken();
        var user = await Mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);
        return user is null ? InvalidToken() : Ok(new { success = true, data = user });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenDto? body,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var tokenId)
            || !Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
            return InvalidToken();
        await Mediator.Send(new LogoutCommand(tokenId, userId, body?.RefreshToken), cancellationToken);
        return Ok(new { success = true, message = "Logout successful" });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenDto? body,
        CancellationToken cancellationToken)
    {
        var session = await Mediator.Send(new RefreshCommand(body?.RefreshToken), cancellationToken);
        return Ok(new { success = true, data = session });
    }

    private UnauthorizedObjectResult InvalidToken() => Unauthorized(new
    {
        success = false, code = "INVALID_TOKEN", message = "Token is invalid, expired or revoked."
    });
}
