using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Contracts.Notifications;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;

namespace SmartParking.NotificationAuditService.Infrastructure.Consumers;

public class PaymentNotificationConsumer : IConsumer<PaymentVerifiedNotificationEvent>
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

    public async Task Consume(ConsumeContext<PaymentVerifiedNotificationEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Consuming payment notification event {EventId} for payment order {PaymentOrderId}, recipient {DriverId}.",
            message.EventId,
            message.PaymentOrderId,
            message.DriverId);

        var command = new ProcessPaymentNotificationCommand
        {
            EventId = message.EventId,
            PaymentOrderId = message.PaymentOrderId,
            DriverId = message.DriverId,
            Amount = message.Amount,
            Currency = message.Currency,
            PaymentMethod = message.PaymentMethod,
            TransactionId = message.TransactionId,
            OccurredAt = message.OccurredAt
        };

        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.IsDuplicate)
        {
            _logger.LogInformation(
                "Consumer acknowledged duplicate payment notification event {EventId}.",
                message.EventId);
        }
        else if (result.Success)
        {
            _logger.LogInformation(
                "Consumer successfully processed payment notification for event {EventId} (NotificationId: {NotificationId}).",
                message.EventId,
                result.NotificationId);
        }
        else
        {
            _logger.LogWarning(
                "Payment notification for event {EventId} resulted in delivery failure: {ErrorMessage}. Persisted independently.",
                message.EventId,
                result.ErrorMessage);
        }
    }
}
