using SmartParking.Contracts.ReservationPayments;
using MassTransit;
using MediatR;
using SmartParking.PaymentService.Application.Reservations;
namespace SmartParking.PaymentService.Infrastructure.Messaging;

public sealed class ReservationDispositionConsumer(IMediator mediator) : IConsumer<RequestReservationDispositionV1>
{
    public Task Consume(ConsumeContext<RequestReservationDispositionV1> context) =>
        mediator.Send(new RequestReservationDisposition(context.Message), context.CancellationToken);
}
