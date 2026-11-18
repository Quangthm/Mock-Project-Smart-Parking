namespace SmartParking.OperationService.Application.Reservations;

public sealed record FacilityBookingFacts(Guid TenantId, Guid SiteId, Guid SlotId, string Version,
    bool EligibleCarSlot, bool ParticipatesInStructureGuard);
