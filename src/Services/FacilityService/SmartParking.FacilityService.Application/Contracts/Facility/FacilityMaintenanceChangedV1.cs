namespace SmartParking.Contracts.Facility;

public sealed record FacilityMaintenanceChangedV1(
    Guid EventId,
    int SchemaVersion,
    Guid MaintenanceId,
    long Revision,
    DateTimeOffset OccurredAtUtc,
    Guid TenantId,
    Guid SiteId,
    string ChangeType,           // "UPSERTED" | "CLEARED"
    string Source,               // "SCHEDULED" | "EMERGENCY"
    string Scope,                // "SITE" | "UNIT" | "SLOT"
    Guid? UnitId,
    IReadOnlyList<Guid> ScopedUnitIds,
    IReadOnlyList<Guid> SlotIds,
    DateTimeOffset StartAtUtc,
    DateTimeOffset? EndAtUtc,
    string Reason);
