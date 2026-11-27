namespace SmartParking.PaymentService.Domain.Pricing;

public sealed record PricingCalculationRequest(
    Guid TenantId,
    Guid SiteId,
    PricingVehicleType VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime,
    PricingPolicySnapshot Policy,
    Guid? SessionId = null,
    Guid? ReservationId = null
);
