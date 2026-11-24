namespace SmartParking.OperationService.Domain.Reservations;

public sealed record CapacityVisit(Guid Id, Guid? ReservationId, Guid? Slot, DateTimeOffset Entry, string Category = "CAR",
    IReadOnlyList<CapacityAllocation>? Allocations = null)
{
    internal (CapacityVisit Visit, Guid? Slot, Guid[] Pins, bool Inconsistent)? LocateAt(DateTimeOffset at,
        DateTimeOffset horizon)
    {
        var active = (Allocations ?? []).Where(a => a.ActiveAt(at)).ToArray();
        // The current walk-in producer's session-only allocation corroborates the observed slot.
        // It does not extend projected occupancy beyond the policy horizon.
        if (at >= horizon && !active.Any(a => a.ReservationId.HasValue)) return null;

        var pins = active.Select(a => a.Slot).Append(Slot ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (pins.Length == 0 && ReservationId is null) return null;
        var inconsistent = pins.Length > 1 || active.Any(a => a.SessionId != Id || a.ReservationId != ReservationId ||
            !a.ReservationId.HasValue && (a.Status != "OCCUPIED" || Slot != a.Slot));
        return (this, pins.Length == 1 ? pins[0] : Slot, pins, inconsistent);
    }
}
