using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

// Facility withdrew capacity (emergency closure or forced maintenance). Operations mirrors the
// closure, blocks new admissions and reconciles the affected commitments.
public sealed record ReconcileFacilityCapacityRestrictedCommand(
    Guid EventId,
    int SchemaVersion,
    Guid TenantId,
    Guid SiteId,
    Guid RestrictionId,
    string Mode,
    string Scope,
    Guid? UnitId,
    IReadOnlyList<Guid> AffectedSlotIds,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    string Reason) : IRequest;