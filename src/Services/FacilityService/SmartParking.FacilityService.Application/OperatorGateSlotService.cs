namespace SmartParking.FacilityService.Application;

// Application entry point; the port owns local atomic persistence. Gate business
// corrections (capacity, settlement and occupancy ownership) remain a separate change.
public sealed class OperatorGateSlotService(IOperatorGateSlotStore store)
{
    public Task<OperatorGateSlot> ClaimAvailableAsync(Guid tenantId, Guid siteId, string vehicleType, Guid? preferredSlotId, Guid? targetUnitId, bool allowReservedSlot, CancellationToken ct) =>
        store.ClaimAvailableAsync(tenantId, siteId, vehicleType, preferredSlotId, targetUnitId, allowReservedSlot, ct);
    public Task ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct) =>
        store.ReleaseAsync(tenantId, siteId, slotId, ct);
}
