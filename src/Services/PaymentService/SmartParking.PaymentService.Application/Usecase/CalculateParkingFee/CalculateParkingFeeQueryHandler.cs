using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;

public sealed class CalculateParkingFeeQueryHandler : IRequestHandler<CalculateParkingFeeQuery, PricingCalculationResult>
{
    private readonly IParkingPricingCalculator _calculator;
    private readonly IParkingTariffPort _tariffPort;
    private readonly IReservationPricingSnapshotPort _reservationPort;
    private readonly ILogger<CalculateParkingFeeQueryHandler> _logger;

    public CalculateParkingFeeQueryHandler(
        IParkingPricingCalculator calculator,
        IParkingTariffPort tariffPort,
        IReservationPricingSnapshotPort reservationPort,
        ILogger<CalculateParkingFeeQueryHandler> logger)
    {
        _calculator = calculator;
        _tariffPort = tariffPort;
        _reservationPort = reservationPort;
        _logger = logger;
    }

    public async Task<PricingCalculationResult> Handle(CalculateParkingFeeQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting parking fee calculation for SiteId: {SiteId}, VehicleType: {VehicleType}, EntryTime: {EntryTime}, ExitTime: {ExitTime}, SessionId: {SessionId}, ReservationId: {ReservationId}",
            request.SiteId, request.VehicleType, request.EntryTime, request.ExitTime, request.SessionId, request.ReservationId);

        var vehicleType = Enum.Parse<PricingVehicleType>(request.VehicleType, ignoreCase: true);

        PricingPolicySnapshot policy;

        // Priority 1: Direct policy override
        if (request.PolicyOverride != null)
        {
            policy = request.PolicyOverride;
            _logger.LogInformation(
                "Using policy override for calculation. TariffId: {TariffId}, TariffName: {TariffName}, PolicyVersion: {PolicyVersion}",
                policy.TariffId, policy.TariffName, policy.PolicyVersion);
        }
        // Priority 2: Reservation accepted snapshot
        else if (request.ReservationId.HasValue)
        {
            var snapshot = await _reservationPort.GetAcceptedPolicySnapshotAsync(
                request.TenantId,
                request.SiteId,
                request.ReservationId.Value,
                cancellationToken);

            if (snapshot == null)
            {
                _logger.LogWarning(
                    "Accepted pricing snapshot for reservation '{ReservationId}' was not found or is corrupted (TenantId: {TenantId}, SiteId: {SiteId})",
                    request.ReservationId.Value, request.TenantId, request.SiteId);

                throw new PricingDomainException(
                    "CORRUPTED_POLICY_SNAPSHOT",
                    $"Accepted pricing snapshot for reservation '{request.ReservationId.Value}' was not found or is corrupted.",
                    "reservationId");
            }

            policy = snapshot;
            _logger.LogInformation(
                "Using accepted reservation pricing snapshot for ReservationId: {ReservationId}, TariffId: {TariffId}, PolicyVersion: {PolicyVersion}",
                request.ReservationId.Value, policy.TariffId, policy.PolicyVersion);
        }
        // Priority 3: Active walk-in tariff
        else
        {
            var activePolicy = await _tariffPort.GetActivePolicyAsync(
                request.TenantId,
                request.SiteId,
                vehicleType,
                cancellationToken);

            if (activePolicy == null)
            {
                _logger.LogWarning(
                    "No active tariff found for TenantId: {TenantId}, SiteId: {SiteId}, VehicleType: {VehicleType}",
                    request.TenantId, request.SiteId, vehicleType);

                throw new PricingDomainException(
                    "TARIFF_NOT_FOUND",
                    $"No active tariff found for site '{request.SiteId}' and vehicle type '{vehicleType}'.",
                    "vehicleType");
            }

            policy = activePolicy;
            _logger.LogInformation(
                "Using active walk-in tariff for SiteId: {SiteId}, VehicleType: {VehicleType}. TariffId: {TariffId}, TariffName: {TariffName}, PolicyVersion: {PolicyVersion}",
                request.SiteId, vehicleType, policy.TariffId, policy.TariffName, policy.PolicyVersion);
        }

        var calcRequest = new PricingCalculationRequest(
            TenantId: request.TenantId,
            SiteId: request.SiteId,
            VehicleType: vehicleType,
            EntryTime: request.EntryTime,
            ExitTime: request.ExitTime,
            Policy: policy,
            SessionId: request.SessionId,
            ReservationId: request.ReservationId);

        var result = _calculator.Calculate(calcRequest);

        _logger.LogInformation(
            "Completed parking fee calculation. FinalAmountVnd: {FinalAmountVnd}, TotalParkedMinutes: {TotalParkedMinutes:F2}, IsFreeStay: {IsFreeStay}, AppliedTariffId: {AppliedTariffId}, AppliedPolicyVersion: {AppliedPolicyVersion}",
            result.FinalAmountVnd, result.TotalParkedMinutes, result.IsFreeStay, result.AppliedTariffId, result.AppliedPolicyVersion);

        return result;
    }
}
