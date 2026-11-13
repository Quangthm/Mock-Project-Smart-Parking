using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Usecase.Management;
using UserService.Application.Usecase.IdentityAccess;
namespace UserService.API.Controllers;

[ApiController,Route("api/auth"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AccountWorkflowController(IAccountWorkflows workflows,AuthSessionService sessions,IMediator mediator, ICurrentActor actor):ControllerBase
{
    private Guid Actor => actor.UserId ?? throw UserService.Application.Common.Models.Exceptions.AuthException.InvalidToken();

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
    [AllowAnonymous, HttpPost("operators/simulate-vneid"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> SimulateOperatorVNeId([FromBody] SimulatedVNeIdRequest body, [FromServices] ISender sender, CancellationToken ct) =>
        Ok(new { success = true, data = await sender.Send(new UserService.Application.Usecase.VNeIdSimulation.SimulateOperatorVNeIdCommand(body), ct) });
    [Authorize, HttpGet("vneid/status")]
    public async Task<IActionResult> VNeIdStatus([FromServices] ISender sender, CancellationToken ct) =>
        Ok(new { success = true, data = await sender.Send(new UserService.Application.Usecase.VNeIdSimulation.GetVNeIdStatusQuery(Actor), ct) });
    [Authorize, HttpGet("mfa/status")]
    public async Task<IActionResult> MfaStatus(CancellationToken ct) =>
        Ok(new { success = true, data = await workflows.MfaStatusAsync(Actor, ct) });

    [Authorize, HttpPost("mfa/setup"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> Setup(MfaVerificationDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new SetupOwnMfaCommand(body), ct) });

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
