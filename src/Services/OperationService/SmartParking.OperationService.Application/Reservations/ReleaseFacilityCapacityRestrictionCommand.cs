using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

// Facility released a previously recorded emergency restriction. The mirrored closure stops
// blocking admissions once the release is applied.
public sealed record ReleaseFacilityCapacityRestrictionCommand(
    Guid EventId,
    int SchemaVersion,
    Guid TenantId,
    Guid SiteId,
    Guid RestrictionId,
    IReadOnlyList<Guid> ReleasedSlotIds) : IRequest;