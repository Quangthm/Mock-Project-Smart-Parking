namespace SmartParking.FacilityService.Application;

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