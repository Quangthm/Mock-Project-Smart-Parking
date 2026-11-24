using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public interface IMaintenanceWindowStore
{
    Task<IReadOnlyList<MaintenanceWindow>> ListAsync(OwnerScope owner, Guid siteId, CancellationToken ct);
    Task<MaintenanceWindow?> GetByIdAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct);
    Task<MaintenanceWindow> CreateAsync(OwnerScope owner, Guid siteId, DateTime start, DateTime end, string reason,
        CancellationToken ct, Guid? unitId = null, Guid? slotId = null);
    Task<MaintenanceWindow> ExtendAsync(OwnerScope owner, Guid siteId, Guid windowId, DateTime newEnd, CancellationToken ct);
    Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct);

    // Enforcement lifecycle (DE-13). The defaults keep simple in-memory implementations compiling;
    // PostgresMaintenanceWindowStore overrides every member. A window is never effective until the
    // remote acknowledgement has been persisted via MarkAcknowledgedAsync.
    Task<MaintenanceWindowScope> ResolveScopeAsync(
        Guid tenantId, Guid siteId, Guid? unitId, Guid? slotId, CancellationToken ct) =>
        Task.FromResult(new MaintenanceWindowScope(
            unitId is { } unit ? [unit] : [],
            slotId is { } slot ? [slot] : []));

    Task<MaintenanceWindow> MarkAcknowledgedAsync(
        Guid tenantId, Guid siteId, Guid windowId, Guid enforcementRef, DateTime acknowledgedAt, CancellationToken ct) =>
        throw new NotSupportedException("This maintenance window store cannot record acknowledgements.");

    Task MarkFailedAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct) =>
        throw new NotSupportedException("This maintenance window store cannot record enforcement failures.");

    Task DeletePendingAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct) =>
        throw new NotSupportedException("This maintenance window store cannot discard pending windows.");

    Task<MaintenanceWindow> RevertExtensionAsync(Guid tenantId, Guid siteId, Guid windowId, DateTime previousEnd,
        string previousEnforcementState, Guid? previousEnforcementRef, DateTime? previousAcknowledgedAt,
        CancellationToken ct) =>
        throw new NotSupportedException("This maintenance window store cannot revert extensions.");

    Task ClearEnforcementRefAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct) =>
        throw new NotSupportedException("This maintenance window store cannot clear enforcement references.");

    Task<IReadOnlyList<MaintenanceWindow>> ListPendingEnforcementAsync(DateTime now, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<MaintenanceWindow>>([]);

    Task<IReadOnlyList<MaintenanceWindow>> ListCancelledPendingRemoteSyncAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<MaintenanceWindow>>([]);

    Task<int> NotifyDueBoundariesAsync(DateTime now, CancellationToken ct) => Task.FromResult(0);
}

public sealed record ServiceState(string State, DateTime? ClosedUntil, IReadOnlyList<MaintenanceWindow> Windows);