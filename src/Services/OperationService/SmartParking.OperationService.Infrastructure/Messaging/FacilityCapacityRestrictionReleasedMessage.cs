using MassTransit;

namespace SmartParking.OperationService.Infrastructure.Messaging;

// Mirrors SmartParking.Contracts.Facility.FacilityCapacityRestrictionReleasedV1 (Slice 3A).
[MessageUrn("SmartParking.Contracts.Facility:FacilityCapacityRestrictionReleasedV1")]
public sealed record FacilityCapacityRestrictionReleasedMessage(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid SiteId,
    Guid RestrictionId,
    IReadOnlyList<Guid> ReleasedSlotIds);