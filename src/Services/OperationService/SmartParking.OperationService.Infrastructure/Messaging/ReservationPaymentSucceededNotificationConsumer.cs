using MassTransit;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Messaging;

public sealed class ReservationPaymentSucceededNotificationConsumer(IReservationProgressRelay relay,
    ILogger<ReservationPaymentSucceededNotificationConsumer> logger) : IConsumer<PaymentSucceededV1>
{
    public const string Queue = "smartpark-operation-reservation-payment-notification-v1";

    public Task Consume(ConsumeContext<PaymentSucceededV1> context)
    {
        var payment = context.Message;
        if (payment.SettlementClassification != PaymentSucceededV1.ReviewedReservationOrderPaid ||
            payment.ReferenceType != "RESERVATION" || payment.PaymentMethod != "PAYOS" || payment.Currency != "VND")
            return Task.CompletedTask;

        if (payment.AccountId is not Guid accountId || accountId == Guid.Empty || payment.ReferenceId == Guid.Empty ||
            payment.EventId == Guid.Empty || payment.PaymentOrderId == Guid.Empty ||
            payment.PaymentTransactionId == Guid.Empty || payment.OccurredAt == default || payment.Amount <= 0)
        {
            logger.LogWarning("Classified reservation settlement notification skipped due to invalid evidence. EventId {EventId}",
                payment.EventId);
            return Task.CompletedTask;
        }

        return relay.PaymentSucceededAsync(accountId, new(payment.EventId, payment.ReferenceId, payment.PaymentOrderId,
            payment.PaymentTransactionId, payment.Amount, payment.Currency, payment.OccurredAt), context.CancellationToken);
    }
}
