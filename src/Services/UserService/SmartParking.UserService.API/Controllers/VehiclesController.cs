using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.API.Common;
using UserService.Application.Usecase.Vehicles;

namespace UserService.API.Controllers;

[ApiController, Authorize, Route("api/vehicles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VehiclesController(IMediator mediator, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        ApiResponseFactory.Success(await mediator.Send(new ListOwnVehiclesQuery(), ct)).ToActionResult();

    [HttpGet("registration-claims")]
    public async Task<IActionResult> Claims(CancellationToken ct) =>
        ApiResponseFactory.Success(await mediator.Send(new ListRegistrationClaimsQuery(), ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> Create(SaveVehicleRequest request, CancellationToken ct)
    {
        var outcome = await mediator.Send(mapper.Map<SaveOwnVehicleCommand>(request), ct);
        return outcome.Vehicle is null
            ? ApiResponseFactory.Error("PLATE_EXISTS", "A vehicle with this plate is already registered.", 409).ToActionResult()
            : (ApiResponseFactory.Success(outcome.Vehicle) with { StatusCode = StatusCodes.Status201Created }).ToActionResult();
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveVehicleRequest request, CancellationToken ct)
    {
        var command = mapper.Map<SaveOwnVehicleCommand>(request) with { VehicleId = id };
        var outcome = await mediator.Send(command, ct);
        return ApiResponseFactory.Success(outcome.Vehicle!).ToActionResult();
    }
}
