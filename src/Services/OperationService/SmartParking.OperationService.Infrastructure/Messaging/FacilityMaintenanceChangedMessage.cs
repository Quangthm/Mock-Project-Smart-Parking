using MassTransit;

namespace SmartParking.OperationService.Infrastructure.Messaging;

// Mirrors SmartParking.Contracts.Facility.FacilityMaintenanceChangedV1 so MassTransit routes
// the fanout exchange without adding a cross-service assembly dependency.
[MessageUrn("SmartParking.Contracts.Facility:FacilityMaintenanceChangedV1")]
public sealed record FacilityMaintenanceChangedMessage(
    Guid EventId,
    int SchemaVersion,
    Guid MaintenanceId,
    long Revision,
    DateTimeOffset OccurredAtUtc,
    Guid TenantId,
    Guid SiteId,
    string ChangeType,
    string Source,
    string Scope,
    Guid? UnitId,
    IReadOnlyList<Guid> ScopedUnitIds,
    IReadOnlyList<Guid> SlotIds,
    DateTimeOffset StartAtUtc,
    DateTimeOffset? EndAtUtc,
    string Reason);
