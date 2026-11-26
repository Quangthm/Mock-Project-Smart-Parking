using MassTransit;
using MediatR;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Messaging;

// Transport adapter for FacilityMaintenanceChangedV1: drops malformed or unsupported-version
// envelopes rather than poisoning the queue, and forwards valid changes to Application.
public sealed class FacilityMaintenanceChangedConsumer(
    IMediator? mediator = null) : IConsumer<FacilityMaintenanceChangedMessage>
{
    public async Task Consume(ConsumeContext<FacilityMaintenanceChangedMessage> context)
    {
        if (mediator is null) return;

        var message = context.Message;
        if (message.EventId == Guid.Empty ||
            message.MaintenanceId == Guid.Empty ||
            message.TenantId == Guid.Empty ||
            message.SiteId == Guid.Empty ||
            message.Revision < 1 ||
            message.SchemaVersion != FacilityMaintenanceConsumerTopology.CurrentSchemaVersion ||
            message.ChangeType is not ("UPSERTED" or "CLEARED") ||
            message.EndAtUtc is { } end && end <= message.StartAtUtc)
        {
            return;
        }

        await mediator.Send(new ApplyFacilityMaintenanceChangedCommand(
            message.EventId,
            message.SchemaVersion,
            message.MaintenanceId,
            message.Revision,
            message.OccurredAtUtc,
            message.TenantId,
            message.SiteId,
            message.ChangeType,
            message.Source,
            message.Scope,
            message.UnitId,
            message.ScopedUnitIds ?? [],
            message.SlotIds ?? [],
            message.StartAtUtc,
            message.EndAtUtc,
            message.Reason), context.CancellationToken);
    }
}
