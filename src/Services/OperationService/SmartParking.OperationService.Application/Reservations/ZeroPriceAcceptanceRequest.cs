namespace SmartParking.OperationService.Application.Reservations;

public sealed record ZeroPriceAcceptanceRequest(Guid OperationId, Guid ReservationId, Guid AccountId,
    Guid TenantId, Guid SiteId, Guid VehicleId, Guid SlotId, DateTimeOffset Start, DateTimeOffset End,
    CommercialTerms AcceptedTerms,
    DateTimeOffset ExpiresAt);
