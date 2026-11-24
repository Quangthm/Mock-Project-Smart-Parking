using MassTransit;

namespace SmartParking.OperationService.Infrastructure.Messaging;

// Mirrors SmartParking.Contracts.Facility.FacilityCapacityRestrictedV1 (Slice 3A). The URN must
// stay byte-identical to the Facility contract so both services exchange the same message type.
[MessageUrn("SmartParking.Contracts.Facility:FacilityCapacityRestrictedV1")]
public sealed record FacilityCapacityRestrictedMessage(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid SiteId,
    Guid RestrictionId,
    string Mode,
    string Scope,
    Guid? UnitId,
    IReadOnlyList<Guid> AffectedSlotIds,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    string Reason);