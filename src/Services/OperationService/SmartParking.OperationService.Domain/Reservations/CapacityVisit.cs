namespace SmartParking.OperationService.Domain.Reservations;

public sealed record CapacityVisit(Guid Id, Guid? ReservationId, Guid? Slot, DateTimeOffset Entry, string Category = "CAR",
    IReadOnlyList<CapacityAllocation>? Allocations = null);
