using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Common.Interfaces.Services;
namespace UserService.API.Controllers;

[ApiController,Route("api/auth"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AccountWorkflowController(IAccountWorkflows workflows,AuthSessionService sessions):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
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
    [AllowAnonymous,HttpPost("bootstrap-password"),Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> ChangePassword(ChangeBootstrapPasswordDto body,CancellationToken ct){await workflows.ChangeBootstrapPasswordAsync(body,ct);return Ok(new{success=true});}
    [Authorize,HttpPost("mfa/setup")]
    public async Task<IActionResult> Setup(CancellationToken ct)=>Ok(new{success=true,data=await workflows.SetupMfaAsync(Actor,ct)});
    [Authorize,HttpPost("mfa/enable")]
    public async Task<IActionResult> Enable(VerifyDriverOtpDto body,CancellationToken ct){await workflows.EnableMfaAsync(Actor,body.Code,ct);return Ok(new{success=true});}
    [Authorize,HttpGet("deliveries/{id:guid}")]
    public async Task<IActionResult> Delivery(Guid id,CancellationToken ct)=>Ok(new{success=true,data=await workflows.DeliveryAsync(Actor,id,false,ct)});
    [Authorize,HttpPost("deliveries/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id,CancellationToken ct)=>Ok(new{success=true,data=await workflows.DeliveryAsync(Actor,id,true,ct)});
}
[ApiController,Authorize(Roles="driver"),Route("api/vehicles"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class VehiclesController(IVehicleRegistration vehicles):ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct)=>Ok(new{success=true,data=await vehicles.ListAsync(Actor,ct)});
    [HttpPost] public async Task<IActionResult> Create(VehicleDto body,CancellationToken ct){var v=await vehicles.SaveAsync(Actor,null,body,ct);return StatusCode(201,new{success=true,data=v});}
    [HttpPatch("{id:guid}")] public async Task<IActionResult> Update(Guid id,VehicleDto body,CancellationToken ct)=>Ok(new{success=true,data=await vehicles.SaveAsync(Actor,id,body,ct)});
}
