using MassTransit;
using MediatR;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Usecase.ReservationGate;

namespace SmartParking.OperationService.Infrastructure.Messaging;

public sealed class PaymentSucceededConsumer(IMediator mediator) : IConsumer<PaymentSucceededV1>
{
    public Task Consume(ConsumeContext<PaymentSucceededV1> context)
    {
        var payment = context.Message;
        if (!string.Equals(payment.ReferenceType, "PARKING_SESSION", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;
        return mediator.Send(new SimulatePaidGateDepartureCommand(
            payment.TenantId, payment.SiteId, payment.ReferenceType, payment.ReferenceId,
            payment.PaymentOrderId, payment.OccurredAt), context.CancellationToken);
    }
}
