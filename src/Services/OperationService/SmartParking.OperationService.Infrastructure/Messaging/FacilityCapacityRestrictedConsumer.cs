using MassTransit;
using MediatR;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Messaging;

// Transport adapter only: validates the envelope and hands the fact to the Application layer.
// Malformed or schema-mismatched messages are dropped instead of throwing, so a bad publisher
// cannot poison the queue.
public sealed class FacilityCapacityRestrictedConsumer(
    IMediator? mediator = null) : IConsumer<FacilityCapacityRestrictedMessage>
{
    public async Task Consume(ConsumeContext<FacilityCapacityRestrictedMessage> context)
    {
        if (mediator is null) return;

        var message = context.Message;
        if (message.EventId == Guid.Empty || message.TenantId == Guid.Empty ||
            message.SiteId == Guid.Empty || message.RestrictionId == Guid.Empty ||
            message.SchemaVersion != FacilityCapacityRestrictionConsumerTopology.CurrentSchemaVersion)
        {
            return;
        }

        await mediator.Send(new ReconcileFacilityCapacityRestrictedCommand(
            message.EventId,
            message.SchemaVersion,
            message.TenantId,
            message.SiteId,
            message.RestrictionId,
            message.Mode,
            message.Scope,
            message.UnitId,
            message.AffectedSlotIds,
            message.EffectiveFrom,
            message.EffectiveUntil,
            message.Reason), context.CancellationToken);
    }
}