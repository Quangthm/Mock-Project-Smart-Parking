using SmartParking.Contracts.ReservationPayments;
using MassTransit;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationFactsPublisher(IPublishEndpoint publisher, ISendEndpointProvider endpoints) : IReservationFactsPublisher
{
    public Task EnsureOrderAsync(EnsureReservationOrderV1 command, CancellationToken ct) =>
        SendAsync(ReservationPaymentEndpoints.EnsureOrder, command, ct);
    public Task RequestDispositionAsync(RequestReservationDispositionV1 command, CancellationToken ct) =>
        SendAsync(ReservationPaymentEndpoints.Disposition, command, ct);

    private async Task SendAsync<T>(string queue, T message, CancellationToken ct) where T : class
    {
        var endpoint = await endpoints.GetSendEndpoint(new Uri($"queue:{queue}"));
        await endpoint.Send(message, ct);
    }

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
