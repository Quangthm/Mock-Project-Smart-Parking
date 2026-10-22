using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;

public sealed class CalculateParkingFeeQueryHandler : IRequestHandler<CalculateParkingFeeQuery, PricingCalculationResult>
{
    private readonly IParkingPricingCalculator _calculator;
    private readonly IParkingTariffPort _tariffPort;
    private readonly IReservationPricingSnapshotPort _reservationPort;

    public CalculateParkingFeeQueryHandler(
        IParkingPricingCalculator calculator,
        IParkingTariffPort tariffPort,
        IReservationPricingSnapshotPort reservationPort)
    {
        _calculator = calculator;
        _tariffPort = tariffPort;
        _reservationPort = reservationPort;
    }

    public async Task<PricingCalculationResult> Handle(CalculateParkingFeeQuery request, CancellationToken cancellationToken)
    {
        var vehicleType = Enum.Parse<PricingVehicleType>(request.VehicleType, ignoreCase: true);

        PricingPolicySnapshot policy;

        // Priority 1: Direct policy override
        if (request.PolicyOverride != null)
        {
            policy = request.PolicyOverride;
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
                throw new PricingDomainException(
                    "CORRUPTED_POLICY_SNAPSHOT",
                    $"Accepted pricing snapshot for reservation '{request.ReservationId.Value}' was not found or is corrupted.",
                    "reservationId");
            }

            policy = snapshot;
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
                throw new PricingDomainException(
                    "TARIFF_NOT_FOUND",
                    $"No active tariff found for site '{request.SiteId}' and vehicle type '{vehicleType}'.",
                    "vehicleType");
            }

            policy = activePolicy;
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

        return _calculator.Calculate(calcRequest);
    }
}
