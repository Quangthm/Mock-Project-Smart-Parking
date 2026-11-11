namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public sealed record OperatorGateSlot(Guid Id, string Code, string VehicleType);

public interface IOperatorGateSlotRepository
{
    Task<OperatorGateSlot> ClaimAvailableAsync(Guid tenantId, Guid siteId, string vehicleType, Guid? preferredSlotId, Guid? targetUnitId, bool allowReservedSlot, CancellationToken ct);
    Task ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct);
}
