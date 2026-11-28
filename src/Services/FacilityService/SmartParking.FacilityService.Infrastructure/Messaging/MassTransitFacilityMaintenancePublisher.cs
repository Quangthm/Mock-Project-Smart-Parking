using MassTransit;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Infrastructure.Messaging;

// Publishes maintenance schedule changes through the MassTransit Bus Outbox inside the same
// FacilityOutboxTransaction as structure_maintenance_windows writes.
public sealed class MassTransitFacilityMaintenancePublisher(
    IPublishEndpoint publish) : IFacilityMaintenancePublisher
{
    public async Task StageChangedAsync(FacilityMaintenanceChangedV1 message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.EventId == Guid.Empty)
            throw new ArgumentException("EventId must not be empty.", nameof(message));
        if (message.MaintenanceId == Guid.Empty)
            throw new ArgumentException("MaintenanceId must not be empty.", nameof(message));
        if (message.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(message));
        if (message.SiteId == Guid.Empty)
            throw new ArgumentException("SiteId must not be empty.", nameof(message));
        if (message.Revision < 1)
            throw new ArgumentException("Revision must be at least 1.", nameof(message));
        if (message.ChangeType is not ("UPSERTED" or "CLEARED"))
            throw new ArgumentException("ChangeType must be UPSERTED or CLEARED.", nameof(message));

        await publish.Publish(message, ct);
    }
}
