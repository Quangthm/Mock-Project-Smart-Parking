using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Ports;

public interface IPassPlanFacilityScopePort
{
    Task<OwnerSiteScopeResult> ValidateOwnerSiteAsync(Guid siteId, CancellationToken ct);

    Task<OwnerSiteScopeResult> ValidateOwnerDraftScopeAsync(
        Guid siteId,
        Guid? zoneId,
        IReadOnlyList<PassVehicleType> vehicleTypes,
        IReadOnlyList<Guid> excludedZoneIds,
        CancellationToken ct);

    Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct);
}
