using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Services;

public interface IMaintenanceEnforcementClient
{
    Task<MaintenanceEnforcementResult> ApplyAsync(MaintenanceWindow window, MaintenanceWindowScope scope,
        CancellationToken ct);

    Task<bool> CancelAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct);
}