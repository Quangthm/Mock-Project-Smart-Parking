using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Infrastructure.Messaging;

public sealed class NoOpLayoutInvalidationPublisher : ILayoutInvalidationPublisher
{
    public Task StageAsync(
        Guid tenantId,
        Guid siteId,
        IReadOnlyList<string> affectedResources,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task StageAsync(
        Guid eventId,
        Guid tenantId,
        Guid siteId,
        IReadOnlyList<string> affectedResources,
        CancellationToken ct = default) => Task.CompletedTask;
}
