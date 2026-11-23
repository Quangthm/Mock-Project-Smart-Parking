using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Tests.TestDoubles;

internal sealed class RecordingInvalidationPublisher(List<string> calls) : ILayoutInvalidationPublisher
{
    public List<(Guid TenantId, Guid SiteId, string[] Resources)> Staged { get; } = [];

    public Task StageAsync(Guid tenantId, Guid siteId, IReadOnlyList<string> affectedResources, CancellationToken ct = default)
    {
        calls.Add("stage:" + string.Join(',', affectedResources));
        Staged.Add((tenantId, siteId, [.. affectedResources]));
        return Task.CompletedTask;
    }
}
