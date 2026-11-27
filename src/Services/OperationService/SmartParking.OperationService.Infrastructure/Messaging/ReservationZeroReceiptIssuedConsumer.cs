using SmartParking.Contracts.ReservationPayments;
using MassTransit;
using MediatR;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Usecase.ReservationGate;

namespace SmartParking.OperationService.Infrastructure.Messaging;

public sealed class ReservationZeroReceiptIssuedConsumer(IMediator mediator) : IConsumer<ReservationZeroReceiptIssuedV1>
{
    public Task Consume(ConsumeContext<ReservationZeroReceiptIssuedV1> context) =>
        mediator.Send(new LinkReservationZeroReceipt(context.Message), context.CancellationToken);
}
