using SmartParking.Contracts.ReservationPayments;
using MassTransit;
using MediatR;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Usecase.ReservationGate;

namespace SmartParking.OperationService.Infrastructure.Messaging;

public sealed class ReservationCheckoutChangedConsumer(IReservationProgressRelay relay) : IConsumer<ReservationCheckoutChangedV1>
{
    public Task Consume(ConsumeContext<ReservationCheckoutChangedV1> context) =>
        relay.CheckoutChangedAsync(context.Message.AccountId, context.Message.ReservationId, context.CancellationToken);
}
