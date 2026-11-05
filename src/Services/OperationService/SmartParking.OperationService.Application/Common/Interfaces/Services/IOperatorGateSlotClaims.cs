namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public sealed record ClaimedParkingSlot(Guid Id, string Code, string VehicleType);

public interface IOperatorGateSlotLease : IAsyncDisposable
{
    ClaimedParkingSlot Slot { get; }
    void Commit();
}

public interface IOperatorGateSlotClaims
{
    Task<IOperatorGateSlotLease> ClaimAvailableAsync(
        Guid tenantId,
        Guid siteId,
        string vehicleType,
        Guid? preferredSlotId,
        Guid? targetUnitId,
        bool allowReservedSlot,
        CancellationToken ct);

    Task ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct);
}
