using MassTransit;
using MediatR;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationHoldChangedConsumer(IMediator mediator) : IConsumer<ReservationHoldChangedV1>
{
    public Task Consume(ConsumeContext<ReservationHoldChangedV1> context) =>
        mediator.Send(new ProcessReservationHold(context.Message), context.CancellationToken);
}
