using MassTransit;
using MediatR;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Messaging;

// Transport adapter only: the release fact is idempotent by EventId in the Application handler.
public sealed class FacilityCapacityRestrictionReleasedConsumer(
    IMediator? mediator = null) : IConsumer<FacilityCapacityRestrictionReleasedMessage>
{
    public async Task Consume(ConsumeContext<FacilityCapacityRestrictionReleasedMessage> context)
    {
        if (mediator is null) return;

        var message = context.Message;
        if (message.EventId == Guid.Empty || message.TenantId == Guid.Empty ||
            message.SiteId == Guid.Empty || message.RestrictionId == Guid.Empty ||
            message.SchemaVersion != FacilityCapacityRestrictionConsumerTopology.CurrentSchemaVersion)
        {
            return;
        }

        await mediator.Send(new ReleaseFacilityCapacityRestrictionCommand(
            message.EventId,
            message.SchemaVersion,
            message.TenantId,
            message.SiteId,
            message.RestrictionId,
            message.ReleasedSlotIds), context.CancellationToken);
    }
}