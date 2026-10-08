using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.API.Controllers;

[ApiController]
[Route("api/owner-applications")]
[Authorize(Roles = "admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerApplicationsController(IOwnerRegistrationService registrations) : ControllerBase
{
    private Guid AdminId => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : throw AuthException.InvalidToken();

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(new { success = true, data = await registrations.ListAsync(AdminId, ct) });
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id,CancellationToken ct)
    {
        var application=(await registrations.ListAsync(AdminId,ct)).SingleOrDefault(a=>a.Id==id);
        return application is null?NotFound(new{success=false,code="APPLICATION_NOT_FOUND"}):Ok(new{success=true,data=application});
    }

    [HttpPatch("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewOwnerDto body, CancellationToken ct) =>
        Ok(new { success = true, data = await registrations.ReviewAsync(AdminId, id, body, ct) });
}
