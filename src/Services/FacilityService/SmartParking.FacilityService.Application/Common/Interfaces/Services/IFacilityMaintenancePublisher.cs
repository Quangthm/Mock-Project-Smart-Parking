using SmartParking.Contracts.Facility;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Services;

public interface IFacilityMaintenancePublisher
{
    Task StageChangedAsync(FacilityMaintenanceChangedV1 message, CancellationToken ct = default);
}
