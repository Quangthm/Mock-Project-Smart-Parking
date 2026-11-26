using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.API.Common;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Tests.API.Probes;

[ApiController]
[Route("error-probe")]
public sealed class PaymentErrorProbeController(IMediator mediator) : ControllerBase
{
    [HttpPost("binding")]
    public async Task<IActionResult> Binding(PaymentErrorProbeInput input, CancellationToken cancellationToken) =>
        Ok(new { success = true, data = await mediator.Send(new PaymentErrorProbeCommand(input.Name), cancellationToken) });

    [HttpGet("secure")]
    [Authorize(Roles = "ADMIN")]
    public IActionResult Secure() => NoContent();

    [HttpGet("s2s-secure")]
    [Authorize(Policy = OperationsServiceAuthentication.Policy)]
    public IActionResult S2sSecure() => NoContent();

    [HttpGet("limited")]
    [EnableRateLimiting("probe")]
    public IActionResult Limited() => NoContent();

    [HttpGet("empty")]
    public IActionResult EmptyResponse() => NoContent();

    [HttpGet("pdf")]
    public IActionResult Pdf() => File(new MemoryStream("%PDF-1.4"u8.ToArray()), "application/pdf", "invoice.pdf");

    [HttpGet("controller-error")]
    public IActionResult ControllerError() =>
        new PaymentApiErrorActionResult(PaymentErrorResponse.Error(
            Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
            "MISSING_SESSION_ID",
            "The sessionId query parameter is required."));

    [HttpGet("domain-conflict")]
    public IActionResult DomainConflict() =>
        throw new PaymentDomainException(PaymentDomainException.OrderAlreadyPaid, "This payment order has already been paid.");

    [HttpGet("payos-secret-leak")]
    public IActionResult PayOsSecretLeak() =>
        throw new PaymentDomainException("PAYOS_500", "secret-payos-api-key-and-raw-body");

    [HttpGet("db-concurrency")]
    public IActionResult DbConcurrency() =>
        throw new DbUpdateConcurrencyException("secret concurrency details");

    [HttpGet("unexpected/{id}")]
    public IActionResult Unexpected() =>
        throw new DbUpdateException("secret SQL password name", new Exception("secret-inner"));
}
