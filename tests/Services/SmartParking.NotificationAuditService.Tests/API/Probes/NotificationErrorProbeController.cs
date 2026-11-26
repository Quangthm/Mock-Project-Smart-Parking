using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.API.Common;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

[ApiController]
[Route("error-probe")]
public sealed class NotificationErrorProbeController(IMediator mediator) : ControllerBase
{
    [HttpPost("binding")]
    public async Task<IActionResult> Binding(NotificationErrorProbeInput input, CancellationToken cancellationToken) =>
        Ok(new { success = true, data = await mediator.Send(new NotificationErrorProbeCommand(input.Name), cancellationToken) });

    [HttpGet("secure")]
    [Authorize(Roles = "ADMIN")]
    public IActionResult Secure() => NoContent();

    [HttpGet("limited")]
    [EnableRateLimiting("probe")]
    public IActionResult Limited() => NoContent();

    [HttpGet("empty")]
    public IActionResult EmptyResponse() => NoContent();

    [HttpGet("controller-error")]
    public IActionResult ControllerError() =>
        new NotificationApiErrorActionResult(NotificationErrorResponse.Error(
            StatusCodes.Status400BadRequest,
            "INVALID_AUDIT_FILTER",
            "The supplied audit filter is invalid."));

    [HttpGet("forbidden")]
    public IActionResult Forbidden() =>
        throw new NotificationForbiddenException("secret actor=guid-123 supportCase=secret-case-text");

    [HttpGet("not-found")]
    public IActionResult MissingItem() =>
        throw new KeyNotFoundException("secret-internal-notification-key-999");

    [HttpGet("domain-error")]
    public IActionResult DomainError() =>
        throw new NotificationDomainException("Cannot retry an already delivered notification.");

    [HttpGet("db-concurrency")]
    public IActionResult DbConcurrency() =>
        throw new DbUpdateConcurrencyException("secret concurrency details");

    [HttpGet("unexpected/{id}")]
    public IActionResult Unexpected() =>
        throw new DbUpdateException("secret SQL password actor=secret-actor", new Exception("secret-inner"));
}
