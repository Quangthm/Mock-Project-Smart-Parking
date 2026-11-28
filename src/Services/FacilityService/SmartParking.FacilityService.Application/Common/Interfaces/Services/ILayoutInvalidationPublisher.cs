namespace SmartParking.FacilityService.Application.Common.Interfaces.Services;

public interface ILayoutInvalidationPublisher
{
    Task StageAsync(
        Guid tenantId,
        Guid siteId,
        IReadOnlyList<string> affectedResources,
        CancellationToken ct = default);

    Task StageAsync(
        Guid eventId,
        Guid tenantId,
        Guid siteId,
        IReadOnlyList<string> affectedResources,
        CancellationToken ct = default) =>
        StageAsync(tenantId, siteId, affectedResources, ct);
}
