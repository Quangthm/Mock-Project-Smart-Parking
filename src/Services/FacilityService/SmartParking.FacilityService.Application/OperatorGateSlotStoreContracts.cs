using System.Text.Json;
namespace SmartParking.FacilityService.Application;

public sealed record OperatorGateSlot(Guid Id, string Code, string VehicleType);


public interface IOperatorGateSlotStore
{
    Task<OperatorGateSlot> ClaimAvailableAsync(Guid tenantId, Guid siteId, string vehicleType, Guid? preferredSlotId, Guid? targetUnitId, bool allowReservedSlot, CancellationToken ct);
    Task ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct);
}
