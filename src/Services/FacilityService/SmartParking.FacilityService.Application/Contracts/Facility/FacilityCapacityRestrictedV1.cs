namespace SmartParking.Contracts.Facility;

public sealed record FacilityCapacityRestrictedV1(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid SiteId,
    Guid RestrictionId,
    string Mode,           // "EMERGENCY" | "FORCED_MAINTENANCE"
    string Scope,          // "SITE" | "UNIT" | "SLOT"
    Guid? UnitId,
    IReadOnlyList<Guid> AffectedSlotIds,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    string Reason);