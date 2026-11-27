namespace SmartParking.FacilityService.Application.Reservations;

public sealed record ReservationSlotEligibility(Guid SlotId, string Outcome, Guid? SiteId, Guid? TenantId);
