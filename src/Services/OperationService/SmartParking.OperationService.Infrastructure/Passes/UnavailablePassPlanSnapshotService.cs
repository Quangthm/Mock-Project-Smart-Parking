using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Passes;

namespace SmartParking.OperationService.Infrastructure.Passes;

public sealed class UnavailablePassPlanSnapshotService : IPassPlanSnapshotService
{
    public Task<PassPlanSnapshot> ValidateAndFreezeAsync(PassPlanSnapshotRequest request, CancellationToken ct) =>
        throw new ReservationGateException(503, "PASS_PLAN_SNAPSHOT_UNAVAILABLE",
            "The OperationService pass-plan snapshot capability is not registered yet.");
}
