using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public interface IMaintenanceWindowStore
{
    Task<IReadOnlyList<MaintenanceWindow>> ListAsync(OwnerScope owner, Guid siteId, CancellationToken ct);
    Task<MaintenanceWindow> CreateAsync(OwnerScope owner, Guid siteId, DateTime start, DateTime end, string reason, CancellationToken ct);
    Task<MaintenanceWindow> ExtendAsync(OwnerScope owner, Guid siteId, Guid windowId, DateTime? start, DateTime? end, CancellationToken ct);
    Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct);
}

public sealed record ServiceState(string State, DateTime? ClosedUntil, IReadOnlyList<MaintenanceWindow> Windows);
