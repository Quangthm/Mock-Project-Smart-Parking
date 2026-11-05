using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Authorization;

namespace UserService.API.Controllers;

// Parking forwards the caller's bearer token; Application resolves current resource authority.
[ApiController, Authorize, Route("api/auth/owner-scope")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IdentityScopeController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct, string permission = "PARKING_MANAGE") =>
        Ok(await mediator.Send(new GetOwnerScopeQuery(permission), ct));
}
