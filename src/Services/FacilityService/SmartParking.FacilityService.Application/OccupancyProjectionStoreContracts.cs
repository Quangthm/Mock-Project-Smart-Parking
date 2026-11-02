namespace SmartParking.FacilityService.Application;

// Occupancy projections map OperationService events onto facility-owned parking_slots.
// Application owns the port; Persistence implements it with the database idempotency
// guarantees (processed_events, slot_sequence_watermarks, occupancy_release_ledger) from
// the SPARK-244 delta design (G5). Infra/transport concerns stay out of this contract.
public interface IOccupancyProjectionStore
{
    // Returns true only when the event advanced the projection. Returns false when the event
    // was a duplicate (eventId already processed) or stale (sequence not ahead of the slot
    // watermark) and was intentionally not applied.
    Task<bool> ApplyAsync(OccupancyProjectionEvent projectionEvent, CancellationToken ct);
}

// Transport-neutral projection fact. slotId/sessionId are null for walk-in occupancy events
// that have no slot; physicalState carries the projected slot state when present.
public sealed record OccupancyProjectionEvent(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    long SequenceNumber,
    Guid SiteId,
    Guid? SlotId,
    Guid? SessionId,
    string? PhysicalState);
