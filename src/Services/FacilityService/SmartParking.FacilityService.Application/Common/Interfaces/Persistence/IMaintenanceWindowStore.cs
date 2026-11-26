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

    Task<MaintenanceWindowScope> ResolveScopeAsync(
        Guid tenantId, Guid siteId, Guid? unitId, Guid? slotId, CancellationToken ct) =>
        Task.FromResult(new MaintenanceWindowScope(
            unitId is { } unit ? [unit] : [],
            slotId is { } slot ? [slot] : []));

    Task<int> NotifyDueBoundariesAsync(DateTime now, CancellationToken ct) => Task.FromResult(0);
}

public sealed record ServiceState(string State, DateTime? ClosedUntil, IReadOnlyList<MaintenanceWindow> Windows);