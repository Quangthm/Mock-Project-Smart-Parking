namespace SmartParking.OperationService.Application.Discovery;

public sealed record CandidateSlot(Guid SlotId, string Outcome, string PhysicalState, DateTimeOffset SlotUpdatedAt);
