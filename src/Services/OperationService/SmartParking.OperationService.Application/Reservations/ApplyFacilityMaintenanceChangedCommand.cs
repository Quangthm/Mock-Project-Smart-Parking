using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ApplyFacilityMaintenanceChangedCommand(
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
    string Reason) : IRequest;
