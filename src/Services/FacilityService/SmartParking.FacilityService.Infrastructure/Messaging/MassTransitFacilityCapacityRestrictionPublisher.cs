using MassTransit;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Infrastructure.Messaging;

// Publishes the emergency-restriction integration events through the MassTransit Bus Outbox.
// The caller stages these inside the same FacilityOutboxTransaction as the local Npgsql writes
// so the restriction row and the event either both become visible or neither does.
public sealed class MassTransitFacilityCapacityRestrictionPublisher(
    IPublishEndpoint publish) : IFacilityCapacityRestrictionPublisher
{
    public async Task StageRestrictedAsync(FacilityCapacityRestrictedV1 message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.EventId == Guid.Empty)
            throw new ArgumentException("EventId must not be empty.", nameof(message));
        if (message.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(message));
        if (message.SiteId == Guid.Empty)
            throw new ArgumentException("SiteId must not be empty.", nameof(message));
        if (message.RestrictionId == Guid.Empty)
            throw new ArgumentException("RestrictionId must not be empty.", nameof(message));

        await publish.Publish(message, ct);
    }

    public async Task StageReleasedAsync(FacilityCapacityRestrictionReleasedV1 message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.EventId == Guid.Empty)
            throw new ArgumentException("EventId must not be empty.", nameof(message));
        if (message.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(message));
        if (message.SiteId == Guid.Empty)
            throw new ArgumentException("SiteId must not be empty.", nameof(message));
        if (message.RestrictionId == Guid.Empty)
            throw new ArgumentException("RestrictionId must not be empty.", nameof(message));

        await publish.Publish(message, ct);
    }
}