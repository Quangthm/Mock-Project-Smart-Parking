using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Tests.TestDoubles;

internal sealed class FakeGateSlotRepository(
    OperatorGateSlot? slot,
    bool released,
    bool throwOnClaim = false) : IOperatorGateSlotRepository
{
    public List<string> Calls { get; set; } = [];

    public Task<OperatorGateSlot> ClaimAvailableAsync(
        Guid tenantId,
        Guid siteId,
        string vehicleType,
        Guid? preferredSlotId,
        Guid? targetUnitId,
        bool allowReservedSlot,
        CancellationToken ct)
    {
        if (throwOnClaim)
            throw new StructureException("NO_AVAILABLE_SLOT", "No eligible slot.");
        Calls.Add("claim");
        return Task.FromResult(slot!);
    }

    public Task<bool> ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct)
    {
        Calls.Add("release");
        return Task.FromResult(released);
    }
}
