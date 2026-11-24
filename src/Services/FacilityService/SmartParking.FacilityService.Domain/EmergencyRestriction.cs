namespace SmartParking.FacilityService.Domain;

// An immediate physical closure recorded by Facility. Unlike a planned maintenance window it is
// effective as soon as it is written: admission stops even if Operations reconciliation fails.
// Occupied bays are never evacuated; they stay OCCUPIED and are still listed in AffectedSlotIds
// so any future reservation targeting them is reconciled once the vehicle leaves.
public sealed record EmergencyRestriction(
    Guid Id,
    Guid TenantId,
    Guid SiteId,
    string Mode,
    string Scope,
    Guid? UnitId,
    Guid? SlotId,
    IReadOnlyList<Guid> AffectedSlotIds,
    string Reason,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReleasedAt = null,
    Guid? ReleasedBy = null);