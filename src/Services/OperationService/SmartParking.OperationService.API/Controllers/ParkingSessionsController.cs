using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.Usecase.ReservationGate;

namespace SmartParking.OperationService.API.Controllers;

[Route("api/parking-sessions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ParkingSessionsController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet("my-history")]
    public async Task<IActionResult> MyHistory(
        [FromQuery] string? status,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var rawAccountId = User.FindFirst("account_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (string.IsNullOrWhiteSpace(rawAccountId) || !Guid.TryParse(rawAccountId, out var accountId))
        {
            // If caller provides X-Account-Id header (e.g. gateway or internal routing), respect it if authenticated
            var headerAccount = Request.Headers["X-Account-Id"].ToString();
            if (string.IsNullOrWhiteSpace(headerAccount) || !Guid.TryParse(headerAccount, out accountId))
            {
                return Unauthorized(new { success = false, message = "Driver authorization required." });
            }
        }

        var sessions = await Mediator.Send(new ListDriverParkingSessionsQuery(accountId, status, limit), ct);
        return Ok(new { success = true, data = sessions });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid siteId, CancellationToken ct)
    {
        var sessions = await Mediator.Send(new ListParkingSessionsQuery(siteId), ct);
        return Ok(new { success = true, data = sessions });
    }

    [HttpGet("walk-in/lookup")]
    public async Task<IActionResult> LookupWalkIn(
        [FromQuery] Guid siteId,
        [FromQuery] string plate,
        CancellationToken ct)
    {
        var session = await Mediator.Send(new FindWalkInSessionQuery(siteId, plate), ct);
        return Ok(new { success = true, data = session });
    }

    [HttpPost("walk-in/check-in")]
    public async Task<IActionResult> CheckInWalkIn(
        [FromQuery] Guid siteId,
        [FromBody] WalkInGateActionRequest body,
        CancellationToken ct)
    {
        var session = await Mediator.Send(
            new CheckInWalkInCommand(siteId, body.PlateNumber, body.ImageData, body.VehicleType), ct);
        return Ok(new { success = true, data = session });
    }

    [HttpPost("{sessionId:guid}/walk-in/check-out")]
    public async Task<IActionResult> CheckOutWalkIn(
        [FromRoute] Guid sessionId,
        [FromQuery] Guid siteId,
        [FromBody] WalkInGateActionRequest body,
        CancellationToken ct)
    {
        var session = await Mediator.Send(
            new CheckOutWalkInCommand(siteId, sessionId, body.PlateNumber, body.ImageData), ct);
        return Ok(new { success = true, data = session });
    }
}

public sealed record WalkInGateActionRequest(string PlateNumber, string? ImageData = null, string? VehicleType = null);
