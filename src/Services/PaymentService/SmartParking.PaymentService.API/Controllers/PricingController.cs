using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.Application.DTOs;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.CreateReservationQuote;
using SmartParking.PaymentService.Application.Usecase.Reservations.Queries.PreviewReservationPrice;

namespace SmartParking.PaymentService.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/pricing")]
public sealed class PricingController : ControllerBase
{
    private readonly IMediator _mediator;

    public PricingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [AllowAnonymous]
    [HttpPost("reservations/preview")]
    [ProducesResponseType(typeof(ReservationPricePreviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewReservationPrice(
        [FromBody] ReservationPricePreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        var query = new PreviewReservationPriceQuery(request);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new
        {
            success = true,
            data = result
        });
    }

    [HttpPost("reservations/quote")]
    [ProducesResponseType(typeof(CommercialTermsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReservationQuote(
        [FromBody] CreateReservationQuoteRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new CreateReservationQuoteCommand(request);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new
        {
            success = true,
            data = result
        });
    }

    [HttpPost("calculate")]
    [ProducesResponseType(typeof(CalculatePricingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Calculate(
        [FromBody] CalculatePricingRequestDto request,
        CancellationToken cancellationToken)
    {
        var isTrustedCaller = User.IsInRole("ADMIN") || User.IsInRole("OPERATOR");

        var query = new CalculateParkingFeeQuery(
            TenantId: request.TenantId,
            SiteId: request.SiteId,
            VehicleType: request.VehicleType,
            EntryTime: request.EntryTime,
            ExitTime: request.ExitTime,
            SessionId: request.SessionId,
            ReservationId: request.ReservationId,
            PolicyOverride: request.PolicyOverride,
            IsTrustedInternalCaller: isTrustedCaller
        );

        var result = await _mediator.Send(query, cancellationToken);

        var response = new CalculatePricingResponseDto(
            Currency: result.Currency,
            LocalEntryTime: result.LocalEntryTime,
            LocalExitTime: result.LocalExitTime,
            TimeZoneId: result.TimeZoneId,
            TotalParkedMinutes: result.TotalParkedMinutes,
            InvoiceRoundedMinutes: result.InvoiceRoundedMinutes,
            BilledMinutes: result.BilledMinutes,
            FreeParkingGraceMinutes: result.FreeParkingGraceMinutes,
            IsFreeStay: result.IsFreeStay,
            UnroundedTotalVnd: result.UnroundedTotalVnd,
            FinalAmountVnd: result.FinalAmountVnd,
            RoundingMode: result.RoundingMode.ToString(),
            AppliedTariffId: result.AppliedTariffId,
            AppliedTariffName: result.AppliedTariffName,
            AppliedPolicyVersion: result.AppliedPolicyVersion,
            Segments: result.Segments.Select(s => new PricingSegmentBreakdownDto(
                SegmentIndex: s.SegmentIndex,
                LocalDate: s.LocalDate,
                SegmentStart: s.SegmentStart,
                SegmentEnd: s.SegmentEnd,
                BlockCode: s.BlockCode,
                OverlapMinutes: s.OverlapMinutes,
                RatePerHourVnd: s.RatePerHourVnd,
                RawChargeVnd: s.RawChargeVnd
            )).ToList()
        );

        return Ok(new
        {
            success = true,
            data = response
        });
    }
}
