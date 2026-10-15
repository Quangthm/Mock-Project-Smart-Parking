using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using UserService.Application.Usecase.IdentityAccess;

namespace UserService.API.Controllers;

// Parking forwards the caller's bearer token. No shared DbContext or signing private key.
[ApiController, Authorize(Roles = "owner"), Route("api/auth/owner-scope")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IdentityScopeController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct, string permission="PARKING_MANAGE")
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)) return Unauthorized();
        return Ok(await sender.Send(new OwnerScopeQuery(userId, permission), ct));
    }
}
