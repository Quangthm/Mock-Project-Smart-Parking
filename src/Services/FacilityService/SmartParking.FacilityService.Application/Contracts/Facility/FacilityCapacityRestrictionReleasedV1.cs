namespace SmartParking.Contracts.Facility;

public sealed record FacilityCapacityRestrictionReleasedV1(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid SiteId,
    Guid RestrictionId,
    IReadOnlyList<Guid> ReleasedSlotIds);