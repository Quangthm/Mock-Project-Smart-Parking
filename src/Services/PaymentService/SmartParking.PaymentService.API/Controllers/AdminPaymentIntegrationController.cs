using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.Application.Integrations;

namespace SmartParking.PaymentService.API.Controllers;

/// <summary>
/// Request body for PUT /api/v1/admin/integrations/payment.
/// A null credential means "keep the stored value"; blank strings are rejected.
/// </summary>
public sealed record UpdatePaymentIntegrationRequest(
    string? Mode,
    string? BaseUrl,
    string? ClientId = null,
    string? ApiKey = null,
    string? ChecksumKey = null);

/// <summary>
/// Admin endpoints for managing the PayOS payment gateway integration.
/// Credentials are stored isolated per mode and are never returned in clear text.
/// GET/PUT management endpoints are deferred from the MVP under SPARK-340 P1-R1.
/// Retain the actions and authorization metadata; Payment runtime dependencies remain active.
/// </summary>
[NonController]
[ApiController]
[Route("api/v1/admin/integrations/payment")]
[Authorize(AuthenticationSchemes = "UserServiceJwt", Roles = "admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPaymentIntegrationController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminPaymentIntegrationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetPaymentIntegrationQuery(), cancellationToken);
        return Ok(new { success = true, data });
    }

    [HttpPut]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        [FromBody] UpdatePaymentIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePaymentIntegrationCommand(
            Mode: request.Mode ?? string.Empty,
            BaseUrl: request.BaseUrl ?? string.Empty,
            ClientId: request.ClientId,
            ApiKey: request.ApiKey,
            ChecksumKey: request.ChecksumKey,
            RemoteIp: HttpContext.Connection.RemoteIpAddress?.ToString());

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new
        {
            success = true,
            message = result.Message,
            data = result.Data
        });
    }
}