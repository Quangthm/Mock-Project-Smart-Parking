using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Common.Interfaces.Services;
using MediatR;
using UserService.Application.Usecase.IdentityAccess;

namespace UserService.API.Controllers;

[ApiController, Route("api/operators"), Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class OperatorsController(ISender sender) : ControllerBase
{
    [HttpPost("{id:guid}/invitation"), Authorize(Roles="owner"), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("DriverOtp")]
    public async Task<IActionResult> Reissue(Guid id, CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var actor)) return Unauthorized();
        return Ok(new { success=true, data=await sender.Send(new ReissueOperatorInvitationCommand(actor,id),ct) });
    }
    [HttpGet, Authorize(Roles="owner")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var ownerId)) return Unauthorized();
        return Ok(new { success=true,data=await sender.Send(new ListOperatorsQuery(ownerId),ct) });
    }
    [HttpGet("me"), Authorize(Roles="operator")]
    public async Task<IActionResult> Assignments(CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var operatorId)) return Unauthorized();
        return Ok(new { success=true,data=await sender.Send(new OperatorAssignmentsQuery(operatorId),ct) });
    }
    [HttpGet("slot-scope"),Authorize(Roles="operator")]
    public async Task<IActionResult> SlotScope(Guid siteId,CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var actor))return Unauthorized();
        return Ok(await sender.Send(new OperatorSlotScopeQuery(actor,siteId),ct));
    }
}
