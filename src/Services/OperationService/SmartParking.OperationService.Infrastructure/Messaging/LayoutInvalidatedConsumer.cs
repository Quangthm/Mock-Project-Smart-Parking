using System.Text.Json;
using MassTransit;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Infrastructure.Messaging;

public sealed class LayoutInvalidatedConsumer(
    IRealtimeLayoutBroadcaster? broadcaster = null) : IConsumer<FacilityLayoutInvalidatedMessage>
{
    public async Task Consume(ConsumeContext<FacilityLayoutInvalidatedMessage> context)
    {
        if (broadcaster is null) return;
        var message = context.Message;
        if (message.EventId == Guid.Empty ||
            message.TenantId == Guid.Empty ||
            message.SiteId == Guid.Empty ||
            message.SchemaVersion != FacilityLayoutInvalidationTopology.CurrentSchemaVersion)
        {
            return;
        }

        var knownResources = FacilityLayoutInvalidationTopology.NormalizeKnownResources(message.AffectedResources);
        if (knownResources.Length == 0) return;

        var payload = JsonSerializer.Serialize(new
        {
            tenantId = message.TenantId,
            affectedResources = knownResources
        });

        await broadcaster.BroadcastAsync(
            new OutboxMessage(
                message.EventId,
                "SnapshotRequired",
                message.OccurredAt,
                0,
                message.SiteId,
                payload),
            context.CancellationToken);
    }
}
