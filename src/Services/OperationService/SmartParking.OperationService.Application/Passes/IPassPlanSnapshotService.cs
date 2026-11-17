namespace SmartParking.OperationService.Application.Passes;

public interface IPassPlanSnapshotService
{
    Task<PassPlanSnapshot> ValidateAndFreezeAsync(PassPlanSnapshotRequest request, CancellationToken ct);
}
