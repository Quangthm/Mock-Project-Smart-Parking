namespace SmartParking.FacilityService.Application.Discovery;

public sealed record DiscoveryCandidateSlot(Guid SlotId, string Outcome, string PhysicalState, DateTimeOffset SlotUpdatedAt);
