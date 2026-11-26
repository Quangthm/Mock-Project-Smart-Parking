using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Usecase.ReservationGate;

namespace SmartParking.OperationService.Infrastructure.Messaging;

public sealed class ReservationPaymentSucceededConsumer(IMediator mediator, ILogger<ReservationPaymentSucceededConsumer> logger) : IConsumer<PaymentSucceededV1>
{
    public Task Consume(ConsumeContext<PaymentSucceededV1> context)
    {
        var payment = context.Message;
        if (!string.Equals(payment.ReferenceType, "RESERVATION", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;
        logger.LogInformation("Reservation settlement event consumed. EventId {EventId}, ReservationId {ReservationId}, PaymentOrderId {PaymentOrderId}, PaymentTransactionId {PaymentTransactionId}",
            payment.EventId, payment.ReferenceId, payment.PaymentOrderId, payment.PaymentTransactionId);
        return mediator.Send(new RecordReservationPaymentCommand(new ReservationPaymentEvidence(
            payment.EventId, payment.PaymentOrderId, payment.PaymentTransactionId, payment.ReferenceId,
            payment.AccountId ?? Guid.Empty, payment.TenantId, payment.SiteId, payment.Amount, payment.Currency,
            payment.PaymentMethod, payment.PlateNumber, payment.OccurredAt)), context.CancellationToken);
    }
}
