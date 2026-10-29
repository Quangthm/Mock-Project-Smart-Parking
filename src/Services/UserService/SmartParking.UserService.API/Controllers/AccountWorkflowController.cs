using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Usecase.Management;
using MediatR;
using UserService.Application.Usecase.IdentityAccess;
namespace UserService.API.Controllers;

[ApiController,Route("api/auth"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AccountWorkflowController(IAccountWorkflows workflows,AuthSessionService sessions,IMediator mediator):ControllerBase
{
    [AllowAnonymous,HttpPost("otp/request"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> RequestOtp(OtpLoginDto body,CancellationToken ct)=>Ok(new{success=true,data=await workflows.RequestLoginAsync(body.Contact,ct)});
    [AllowAnonymous,HttpPost("otp/login"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> Complete(CompleteOtpLoginDto body,CancellationToken ct)=>Ok(new{success=true,data=await workflows.CompleteLoginAsync(body,sessions,ct)});
    [AllowAnonymous,HttpPost("register/owner/verify"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> Verify(CompleteOtpLoginDto body,CancellationToken ct){await workflows.VerifyOwnerAsync(body.ChallengeId,body.Code,ct);return Ok(new{success=true});}
    [AllowAnonymous,HttpPost("register/owner/resend"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> Resend(ResendDriverOtpDto body,CancellationToken ct)=>Ok(new{success=true,data=await workflows.ResendOwnerAsync(body.RegistrationId,ct)});
    [AllowAnonymous,HttpPost("register/owner/recover"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> RecoverOwner(OtpLoginDto body,CancellationToken ct)=>Ok(new{success=true,data=await workflows.RecoverOwnerAsync(body.Contact,ct)});
    [AllowAnonymous,HttpPost("register/owner/setup-recovery/request"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> RequestOwnerSetupRecovery(OtpLoginDto body,CancellationToken ct)=>
        Ok(new{success=true,data=await workflows.RequestOwnerSetupRecoveryAsync(body.Contact,ct)});
    [AllowAnonymous,HttpPost("register/owner/setup-recovery/complete"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> CompleteOwnerSetupRecovery(CompleteOtpLoginDto body,CancellationToken ct)=>
        Ok(new{success=true,data=await workflows.CompleteOwnerSetupRecoveryAsync(body,ct)});
    [AllowAnonymous,HttpPost("bootstrap-password"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> ChangePassword(ChangeBootstrapPasswordDto body,[FromServices] ISender sender,CancellationToken ct)
    {await sender.Send(new ActivateOperatorCommand(body),ct);return Ok(new{success=true});}
    [Authorize, HttpGet("mfa/status")]
    public async Task<IActionResult> MfaStatus(CancellationToken ct) =>
        Ok(new { success = true, data = await workflows.MfaStatusAsync(Actor, ct) });

    [Authorize, HttpPost("mfa/setup"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> Setup(MfaVerificationDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await workflows.SetupMfaAsync(Actor, body, ct) });

    [Authorize, HttpPost("mfa/enable"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> Enable(VerifyDriverOtpDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await workflows.EnableMfaAsync(Actor, body.Code, ct) });

    [Authorize, HttpPost("mfa/disable"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> Disable(MfaVerificationDto body, CancellationToken ct)
    {
        await workflows.DisableMfaAsync(Actor, body, ct);
        return Ok(new { success = true });
    }

    [Authorize, HttpPost("mfa/recovery-codes"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> RegenerateRecoveryCodes(MfaVerificationDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await workflows.RegenerateRecoveryCodesAsync(Actor, body, ct) });

    [AllowAnonymous, HttpPost("mfa/login"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> CompleteMfaLogin(CompleteMfaLoginDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await workflows.CompleteMfaLoginAsync(body, sessions, ct) });
    [Authorize,HttpGet("deliveries/{id:guid}")]
    public async Task<IActionResult> Delivery(Guid id,CancellationToken ct)=>Ok(new{success=true,data=await mediator.Send(new GetWorkflowDeliveryQuery(id),ct)});
    [Authorize,HttpPost("deliveries/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id,CancellationToken ct)=>Ok(new{success=true,data=await mediator.Send(new RetryWorkflowDeliveryCommand(id),ct)});
}
[ApiController,Authorize,Route("api/vehicles"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class VehiclesController(IMediator mediator):ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(new{success=true,data=await mediator.Send(new ListOwnVehiclesQuery(),ct)});
    [HttpPost] public async Task<IActionResult> Create(VehicleDto body,CancellationToken ct){var v=await mediator.Send(new SaveOwnVehicleCommand(null,body),ct);return StatusCode(201,new{success=true,data=v});}
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id,VehicleDto body,CancellationToken ct)=>Ok(new{success=true,data=await mediator.Send(new SaveOwnVehicleCommand(id,body),ct)});
}
