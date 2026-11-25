using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.Application.Reservations;

namespace SmartParking.PaymentService.API.Controllers;

[ApiController]
[Authorize(Policy = OperationsServiceAuthentication.QuotePolicy)]
[Route("internal/reservation-quotes/v1")]
public sealed class ReviewedReservationQuotesController(IMediator mediator, IConfiguration config) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Issue(ReviewedQuoteBinding binding, CancellationToken ct) =>
        config.GetValue<bool>("ReservationIntegration:ReviewedSettlementEnabled")
            ? Ok(new { success = true, data = await mediator.Send(new IssueReviewedQuote(binding), ct) })
            : StatusCode(StatusCodes.Status503ServiceUnavailable);

    [HttpGet("{quoteId:guid}")]
    public async Task<IActionResult> Read(Guid quoteId, [FromQuery] Guid accountId, CancellationToken ct) =>
        Ok(new { success = true, data = await mediator.Send(new ReadReviewedQuote(accountId, quoteId), ct) });
}
