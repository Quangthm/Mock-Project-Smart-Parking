using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;

public class ProcessPaymentNotificationCommandHandler
    : IRequestHandler<ProcessPaymentNotificationCommand, NotificationDeliveryResult>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPaymentNotificationCommandHandler> _logger;

    public ProcessPaymentNotificationCommandHandler(
        INotificationRepository notificationRepository,
        INotificationDispatcher dispatcher,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPaymentNotificationCommandHandler> logger)
    {
        _notificationRepository = notificationRepository;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotificationDeliveryResult> Handle(
        ProcessPaymentNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"PAY_{request.EventId}_{request.DriverId}";

        // 1. Deduplication check
        var existing = await _notificationRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation(
                "Duplicate payment notification event {EventId} for driver {DriverId}. Acknowledging without duplication.",
                request.EventId,
                request.DriverId);

            return NotificationDeliveryResult.Duplicate(existing.Id);
        }

        // 2. Build payload (safe: amount, currency, method, transactionId)
        var payloadData = new
        {
            paymentOrderId = request.PaymentOrderId,
            amount = request.Amount,
            currency = request.Currency,
            paymentMethod = request.PaymentMethod,
            transactionId = request.TransactionId,
            occurredAt = request.OccurredAt
        };
        var payloadJson = JsonSerializer.Serialize(payloadData);

        // 3. Create durable notification entity
        var notification = DurableNotification.Create(
            originatingEvent: "PaymentVerified",
            idempotencyKey: idempotencyKey,
            recipientId: request.DriverId,
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_SUCCESS",
            payload: payloadJson);

        await _notificationRepository.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created durable notification {NotificationId} for payment order {PaymentOrderId}, recipient {DriverId}.",
            notification.Id,
            request.PaymentOrderId,
            request.DriverId);

        // 4. Dispatch notification through configured channel
        try
        {
            var dispatchResult = await _dispatcher.DispatchAsync(notification, cancellationToken);
            return dispatchResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected delivery dispatcher error for notification {NotificationId}. Recording failure independently.",
                notification.Id);

            notification.RecordFailure(ex.Message);
            await _notificationRepository.UpdateAsync(notification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return NotificationDeliveryResult.Failed(notification.Id, notification.Attempts, ex.Message);
        }
    }
}
