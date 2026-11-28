using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Infrastructure.Messaging;

public sealed class NoOpFacilityMaintenancePublisher : IFacilityMaintenancePublisher
{
    public Task StageChangedAsync(FacilityMaintenanceChangedV1 message, CancellationToken ct = default) =>
        Task.CompletedTask;
}
