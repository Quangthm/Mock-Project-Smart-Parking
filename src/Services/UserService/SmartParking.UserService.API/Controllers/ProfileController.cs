using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.API.Common;
using UserService.Application.Profiles.Commands;
using UserService.Application.Profiles.Contracts;
using UserService.Application.Profiles.Queries;

namespace UserService.API.Controllers;

/// <summary>
/// The caller's own profile. Every role may read and change the information it is allowed
/// to change; no endpoint here can reach another account.
/// </summary>
[ApiController]
[Route("api/auth/me")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ApiResponseFactory.Success(await mediator.Send(new GetOwnProfileQuery(), cancellationToken)).ToActionResult();

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateOwnProfileRequest body, CancellationToken cancellationToken) =>
        ApiResponseFactory.Success(await mediator.Send(
            new UpdateOwnProfileCommand(body.FullName ?? string.Empty), cancellationToken)).ToActionResult();

}
