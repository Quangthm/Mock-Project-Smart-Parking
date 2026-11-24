namespace SmartParking.OperationService.Domain;

/// <summary>
/// A live commitment (reservation or open session) that overlaps a requested
/// maintenance window. Returned to the caller so it can surface the conflict.
/// </summary>
public sealed record MaintenanceCommitmentConflict(
    Guid Id,
    string Type,
    DateTimeOffset Start,
    DateTimeOffset? End,
    Guid? UnitId,
    Guid? SlotId);