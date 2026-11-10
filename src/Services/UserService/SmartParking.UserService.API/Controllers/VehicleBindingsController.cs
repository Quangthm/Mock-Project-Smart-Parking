using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AutoMapper;
using UserService.API.Common;
using UserService.Application.Usecase.Vehicles;

namespace UserService.API.Controllers;

// Explicit temporary shared-key trust approved for SPARK-243; replace under SERVICE-AUTH-01.
public sealed class VehicleBindingKeyFilter(IConfiguration configuration) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configured = configuration["Services:Key"];
        var supplied = context.HttpContext.Request.Headers["X-Service-Key"];
        if (string.IsNullOrWhiteSpace(configured) || configured.Length < 32 || supplied.Count != 1
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(configured)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied.ToString()))))
            context.Result = ApiResponseFactory.Error("INVALID_SERVICE_KEY", "Service authentication is required.", 401).ToActionResult();
        return Task.CompletedTask;
    }
}

[ApiController, AllowAnonymous, Route("internal/vehicle-bindings"), ServiceFilter(typeof(VehicleBindingKeyFilter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VehicleBindingsController(IMediator mediator, IMapper mapper) : ControllerBase
{
    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve(ResolveVehicleBindingRequest request, CancellationToken ct) =>
        ApiResponseFactory.Success(await mediator.Send(mapper.Map<ResolveVehicleBindingQuery>(request), ct)).ToActionResult();
}
