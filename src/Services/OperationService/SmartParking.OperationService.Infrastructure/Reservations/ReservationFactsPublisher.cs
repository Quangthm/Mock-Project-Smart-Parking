using MassTransit;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationFactsPublisher(IPublishEndpoint publisher) : IReservationFactsPublisher
{
    public Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct) =>
        publisher.Publish(fact, context => { context.MessageId = fact.EventId; context.CorrelationId = fact.SynchronizationToken; }, ct);
    public async Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct)
    {
        await publisher.Publish(fact, context =>
        { context.MessageId = fact.EventId; context.CorrelationId = fact.WorkflowId; }, ct);
        await AvailabilityChangedAsync(new ReservationAvailabilityChangedV1(Guid.NewGuid(), fact.OccurredAt,
            fact.TenantId, fact.SiteId, fact.SlotId, fact.Start, fact.End, fact.State), ct);
    }
    public Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct) =>
        publisher.Publish(fact, context => { context.MessageId = fact.EventId; }, ct);
    public Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct) => publisher.Publish(fact, context =>
    { context.MessageId = fact.EventId; context.CorrelationId = fact.ReservationId; }, ct);
}
