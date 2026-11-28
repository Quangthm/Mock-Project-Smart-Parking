using SmartParking.Contracts.ReservationPayments;
using MassTransit;
using MediatR;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;
namespace SmartParking.PaymentService.Infrastructure.Messaging;

public sealed class EnsureReservationOrderConsumer(IMediator mediator) : IConsumer<EnsureReservationOrderV1>
{
    public Task Consume(ConsumeContext<EnsureReservationOrderV1> context) =>
        mediator.Send(new EnsureReservationDepositOrderCommand(context.Message), context.CancellationToken);
}
