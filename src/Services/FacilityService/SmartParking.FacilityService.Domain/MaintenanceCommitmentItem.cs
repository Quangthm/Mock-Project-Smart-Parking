namespace SmartParking.FacilityService.Domain;

// One reservation or open session that Operations reports as overlapping a maintenance window.
// SESSION entries carry no end and no unit, matching the STRUCTURE_IN_USE commitments contract.
public sealed record MaintenanceCommitmentItem(
    Guid Id,
    string Type,
    DateTimeOffset Start,
    DateTimeOffset? End,
    Guid? UnitId,
    Guid? SlotId);