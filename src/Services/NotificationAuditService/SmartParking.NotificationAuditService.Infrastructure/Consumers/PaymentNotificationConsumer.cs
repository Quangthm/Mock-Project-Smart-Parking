using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;

namespace SmartParking.NotificationAuditService.Infrastructure.Consumers;

public class PaymentNotificationConsumer : IConsumer<PaymentSucceededV1>
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentNotificationConsumer> _logger;

    public PaymentNotificationConsumer(
        IMediator mediator,
        ILogger<PaymentNotificationConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentSucceededV1> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Consuming PaymentSucceededV1 event {EventId} for order {PaymentOrderId}, account {AccountId}, amount {Amount}.",
            message.EventId,
            message.PaymentOrderId,
            message.AccountId,
            message.Amount);

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = message.EventId,
            OccurredAt = message.OccurredAt,
            CorrelationId = message.CorrelationId,
            PaymentOrderId = message.PaymentOrderId,
            PaymentTransactionId = message.PaymentTransactionId,
            PaymentMethod = message.PaymentMethod,
            GatewayReference = message.GatewayReference,
            TenantId = message.TenantId,
            SiteId = message.SiteId,
            ReferenceType = message.ReferenceType,
            ReferenceId = message.ReferenceId,
            PlateNumber = message.PlateNumber,
            AccountId = message.AccountId,
            Amount = message.Amount,
            ParkingFee = message.ParkingFee,
            PenaltyFee = message.PenaltyFee,
            PenaltyReason = message.PenaltyReason,
            Currency = message.Currency
        };

        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.IsDuplicate)
        {
            _logger.LogInformation(
                "Consumer acknowledged duplicate payment event {EventId}.",
                message.EventId);
        }
        else if (result.Success)
        {
            _logger.LogInformation(
                "Consumer successfully processed payment event {EventId} (NotificationId: {NotificationId}).",
                message.EventId,
                result.NotificationId);
        }
        else
        {
            _logger.LogWarning(
                "Payment notification event {EventId} resulted in delivery failure: {ErrorMessage}.",
                message.EventId,
                result.ErrorMessage);
        }
    }
}
