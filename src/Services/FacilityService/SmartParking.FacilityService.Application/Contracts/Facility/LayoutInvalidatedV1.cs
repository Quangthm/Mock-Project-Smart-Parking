namespace SmartParking.Contracts.Facility;

public sealed record LayoutInvalidatedV1(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid SiteId,
    string[] AffectedResources);
