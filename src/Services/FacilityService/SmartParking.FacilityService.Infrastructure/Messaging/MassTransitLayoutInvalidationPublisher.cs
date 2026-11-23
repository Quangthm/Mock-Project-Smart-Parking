using MassTransit;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Infrastructure.Messaging;

public sealed class MassTransitLayoutInvalidationPublisher(
    IPublishEndpoint publish,
    TimeProvider? clock = null) : ILayoutInvalidationPublisher
{
    public Task StageAsync(
        Guid tenantId,
        Guid siteId,
        IReadOnlyList<string> affectedResources,
        CancellationToken ct = default) =>
        StageAsync(Guid.NewGuid(), tenantId, siteId, affectedResources, ct);

    public async Task StageAsync(
        Guid eventId,
        Guid tenantId,
        Guid siteId,
        IReadOnlyList<string> affectedResources,
        CancellationToken ct = default)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId must not be empty.", nameof(eventId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        if (siteId == Guid.Empty)
            throw new ArgumentException("SiteId must not be empty.", nameof(siteId));

        var normalized = LayoutInvalidationResources.NormalizeKnownResources(affectedResources);
        if (normalized.Length == 0)
            throw new ArgumentException("At least one valid AffectedResource is required.", nameof(affectedResources));

        var occurredAt = (clock ?? TimeProvider.System).GetUtcNow();
        await publish.Publish(
            new LayoutInvalidatedV1(
                eventId,
                LayoutInvalidationResources.CurrentSchemaVersion,
                occurredAt,
                tenantId,
                siteId,
                normalized),
            ct);
    }
}
