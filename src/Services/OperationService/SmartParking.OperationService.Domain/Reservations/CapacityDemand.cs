namespace SmartParking.OperationService.Domain.Reservations;

public sealed record CapacityDemand(Guid Id, Guid? Slot, Guid? Unit, DateTimeOffset Start, DateTimeOffset End,
    bool CountsQuota = true, string Category = "CAR", IReadOnlyList<CapacityAllocation>? Allocations = null,
    Guid? TargetSlot = null);
