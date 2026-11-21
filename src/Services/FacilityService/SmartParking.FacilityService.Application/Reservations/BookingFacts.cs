namespace SmartParking.FacilityService.Application.Reservations;

public sealed record BookingFacts(Guid TenantId, Guid SiteId, Guid SlotId, string Version,
    bool EligibleCarSlot, bool ParticipatesInStructureGuard);
