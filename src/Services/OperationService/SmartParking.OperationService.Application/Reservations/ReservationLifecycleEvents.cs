namespace SmartParking.OperationService.Application.Reservations;

// Durable private booking event. The relay authenticates account subscriptions; never send
// AccountId/ReservationId on a public site availability channel.
public sealed record ReservationHoldChangedV1(Guid EventId, DateTimeOffset OccurredAt,
    Guid WorkflowId, Guid ReservationId, Guid AccountId, Guid TenantId, Guid SiteId, Guid SlotId,
    DateTimeOffset Start, DateTimeOffset End, DateTimeOffset HoldExpiresAt, string State, int Version);

// Invalidation only, not an availability promise. No plate, actor, order or payment details.
public sealed record ReservationAvailabilityChangedV1(Guid EventId, DateTimeOffset OccurredAt,
    Guid TenantId, Guid SiteId, Guid SlotId, DateTimeOffset Start, DateTimeOffset End, string Reason);

// Operations has durably applied Facility's committed structure outcome. This is not a
// claim that recovery/refunds completed. Consumers invalidate the whole site's availability.
public sealed record FacilityStructureAppliedV1(Guid EventId, DateTimeOffset OccurredAt,
    Guid SynchronizationToken, Guid TenantId, Guid SiteId, bool SiteActive,
    IReadOnlyList<Guid> RemovedSlotIds, IReadOnlyList<Guid> RemovedUnitIds);
