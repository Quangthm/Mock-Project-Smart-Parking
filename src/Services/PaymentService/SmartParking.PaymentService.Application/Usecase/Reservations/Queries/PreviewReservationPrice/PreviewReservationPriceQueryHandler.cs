using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Queries.PreviewReservationPrice;

public sealed class PreviewReservationPriceQueryHandler
    : IRequestHandler<PreviewReservationPriceQuery, ReservationPricePreviewResponseDto>
{
    private readonly IParkingPricingCalculator _calculator;
    private readonly IParkingTariffPort _tariffPort;
    private readonly ILogger<PreviewReservationPriceQueryHandler> _logger;

    public PreviewReservationPriceQueryHandler(
        IParkingPricingCalculator calculator,
        IParkingTariffPort tariffPort,
        ILogger<PreviewReservationPriceQueryHandler> logger)
    {
        _calculator = calculator;
        _tariffPort = tariffPort;
        _logger = logger;
    }

    public async Task<ReservationPricePreviewResponseDto> Handle(
        PreviewReservationPriceQuery query,
        CancellationToken cancellationToken)
    {
        var req = query.Request;
        _logger.LogInformation(
            "Previewing reservation price for SiteId: {SiteId}, SlotId: {SlotId}, Interval: [{Start} - {End}]",
            req.SiteId, req.RequestedSlotId, req.Start, req.End);

        var vehicleType = PricingVehicleType.CAR;
        if (!string.IsNullOrWhiteSpace(req.VehicleType) &&
            Enum.TryParse<PricingVehicleType>(req.VehicleType, true, out var parsedType))
        {
            vehicleType = parsedType;
        }

        var policy = await _tariffPort.GetActivePolicyAsync(
            req.TenantId ?? Guid.Empty,
            req.SiteId,
            vehicleType,
            cancellationToken);

        if (policy == null)
        {
            _logger.LogWarning(
                "No active tariff found for SiteId: {SiteId}, VehicleType: {VehicleType}",
                req.SiteId, vehicleType);

            throw new PricingDomainException(
                "TARIFF_NOT_FOUND",
                $"No active tariff found for site '{req.SiteId}' and vehicle type '{vehicleType}'.",
                "siteId");
        }

        var calcRequest = new PricingCalculationRequest(
            TenantId: req.TenantId ?? policy.TariffId,
            SiteId: req.SiteId,
            VehicleType: vehicleType,
            EntryTime: req.Start,
            ExitTime: req.End,
            Policy: policy
        );

        var calcResult = _calculator.Calculate(calcRequest);
        decimal bookedAmount = calcResult.FinalAmountVnd;
        decimal depositAmount = bookedAmount; // Default FULL prepayment mode for preview

        bool requiresCompleteSelection = !req.VehicleId.HasValue || string.IsNullOrWhiteSpace(req.PaymentMethod);

        return new ReservationPricePreviewResponseDto(
            BookedAmount: bookedAmount,
            DepositAmount: depositAmount,
            Currency: "VND",
            PricingVersion: policy.PolicyVersion.ToString(),
            RequiresCompleteSelection: requiresCompleteSelection
        );
    }
}
