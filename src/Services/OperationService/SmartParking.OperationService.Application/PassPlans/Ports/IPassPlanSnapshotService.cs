using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.Application.PassPlans.Ports;

public interface IPassPlanSnapshotService
{
    Task<FrozenPassPlanSnapshotDto> ValidateAndFreezeAsync(
        ValidateAndFreezePassPlanRequest request,
        CancellationToken ct = default);
}
