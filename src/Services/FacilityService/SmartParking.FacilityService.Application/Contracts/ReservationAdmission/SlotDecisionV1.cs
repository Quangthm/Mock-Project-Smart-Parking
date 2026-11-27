namespace SmartParking.Contracts.ReservationAdmission;

public sealed record SlotDecisionV1(SlotCheckV1 Request, string Outcome, Guid? SiteId, Guid? TenantId);
