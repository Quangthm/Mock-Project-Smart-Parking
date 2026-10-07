using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.API.Controllers;

[ApiController, Route("api/operators"), Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class OperatorsController(IOperatorProvisioningService operators) : ControllerBase
{
    [HttpGet, Authorize(Roles="owner")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var ownerId)) return Unauthorized();
        return Ok(new { success=true,data=await operators.ListAsync(ownerId,ct) });
    }
    [HttpGet("me"), Authorize(Roles="operator")]
    public async Task<IActionResult> Assignments(CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var operatorId)) return Unauthorized();
        return Ok(new { success=true,data=await operators.AssignmentsAsync(operatorId,ct) });
    }
    [HttpGet("slot-scope"),Authorize(Roles="operator")]
    public async Task<IActionResult> SlotScope(Guid siteId,CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var actor))return Unauthorized();
        if(!await operators.HasPermissionAsync(actor,siteId,"SLOT_OVERRIDE",ct))return Forbid();
        var assignment=(await operators.AssignmentsAsync(actor,ct)).SingleOrDefault(a=>a.SiteId==siteId && a.Permissions.Contains("SLOT_OVERRIDE"));
        return assignment is null?Forbid():Ok(new{userId=actor,tenantIds=new[]{assignment.TenantId}});
    }
}
