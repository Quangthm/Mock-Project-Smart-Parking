using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Tests;

internal sealed class GateTestSlotLease(ClaimedParkingSlot slot) : IOperatorGateSlotLease
{
    public ClaimedParkingSlot Slot => slot;
    public void Commit() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
