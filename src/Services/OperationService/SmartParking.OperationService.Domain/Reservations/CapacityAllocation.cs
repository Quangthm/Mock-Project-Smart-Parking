namespace SmartParking.OperationService.Domain.Reservations;

public sealed record CapacityAllocation(Guid Slot, DateTimeOffset Start, DateTimeOffset End,
    string Status = "RESERVED", Guid? ReservationId = null, Guid? SessionId = null, DateTimeOffset? ScannedAt = null)
{
    public bool ActiveAt(DateTimeOffset at) => Status is ("RESERVED" or "OCCUPIED") && Start <= at && at < End;
    public bool ObservedAssignment(Guid owner, DateTimeOffset at) =>
        Status == "OCCUPIED" && ReservationId == owner && SessionId.HasValue && ScannedAt <= at;
}
