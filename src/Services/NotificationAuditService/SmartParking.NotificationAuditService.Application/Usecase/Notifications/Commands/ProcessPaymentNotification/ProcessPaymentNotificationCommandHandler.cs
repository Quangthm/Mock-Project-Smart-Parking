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
    private readonly ICentralAuditProjectionRepository _auditRepository;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPaymentNotificationCommandHandler> _logger;

    public ProcessPaymentNotificationCommandHandler(
        INotificationRepository notificationRepository,
        ICentralAuditProjectionRepository auditRepository,
        INotificationDispatcher dispatcher,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPaymentNotificationCommandHandler> logger)
    {
        _notificationRepository = notificationRepository;
        _auditRepository = auditRepository;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotificationDeliveryResult> Handle(
        ProcessPaymentNotificationCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Audit Deduplication Check
        var auditAlreadyExists = await _auditRepository.ExistsByEventIdAsync(request.EventId, cancellationToken);
        if (auditAlreadyExists)
        {
            _logger.LogInformation("Audit projection for event {EventId} already recorded. Acknowledging duplicate.", request.EventId);
            return NotificationDeliveryResult.Duplicate(Guid.Empty);
        }

        // 2. Build details payload JSON
        var auditPayload = new
        {
            paymentOrderId = request.PaymentOrderId,
            paymentTransactionId = request.PaymentTransactionId,
            paymentMethod = request.PaymentMethod,
            gatewayReference = request.GatewayReference,
            tenantId = request.TenantId,
            siteId = request.SiteId,
            referenceType = request.ReferenceType,
            referenceId = request.ReferenceId,
            plateNumber = request.PlateNumber,
            accountId = request.AccountId,
            amount = request.Amount,
            parkingFee = request.ParkingFee,
            penaltyFee = request.PenaltyFee,
            penaltyReason = request.PenaltyReason,
            currency = request.Currency,
            occurredAt = request.OccurredAt
        };
        var auditDetailsJson = JsonSerializer.Serialize(auditPayload);

        // 3. Record CentralAuditProjection (Recorded for both Guest and Registered User)
        var isGuest = !request.AccountId.HasValue || request.AccountId.Value == Guid.Empty;
        var actorRole = isGuest ? "GUEST" : "DRIVER";
        var actorId = isGuest ? Guid.Empty : request.AccountId!.Value;

        var projection = CentralAuditProjection.Create(
            originatingService: "PaymentService",
            eventId: request.EventId,
            actorId: actorId,
            actorRole: actorRole,
            action: "PAYMENT_SUCCEEDED",
            resourceType: "PAYMENT_ORDER",
            resourceId: request.PaymentOrderId,
            details: auditDetailsJson,
            occurredAt: request.OccurredAt != default ? request.OccurredAt : DateTimeOffset.UtcNow);

        await _auditRepository.AddAsync(projection, cancellationToken);

        // 4. Branching: Guest Driver vs Registered User
        if (isGuest)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Recorded CentralAuditProjection for Guest payment order {PaymentOrderId}. In-app notification skipped.", request.PaymentOrderId);
            return NotificationDeliveryResult.Delivered(Guid.Empty, 1);
        }

        // Registered User Flow: Create DurableNotification
        var recipientId = request.AccountId!.Value;
        var idempotencyKey = $"PAY_{request.EventId}_{recipientId}";

        var existingNotification = await _notificationRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existingNotification != null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Notification already exists for idempotency key {Key}. Acknowledging duplicate.", idempotencyKey);
            return NotificationDeliveryResult.Duplicate(existingNotification.Id);
        }

        // ICT Timezone (UTC+7) and 15-minute grace period formatting
        var baseTime = request.OccurredAt != default ? request.OccurredAt : DateTimeOffset.UtcNow;
        var graceCutoffUtc = baseTime.AddMinutes(15);
        var ictOffset = TimeSpan.FromHours(7);
        var graceCutoffIct = graceCutoffUtc.ToOffset(ictOffset);
        var exitDeadline = graceCutoffIct.ToString("HH:mm:ss");
        var formattedAmount = string.Format("{0:N0} {1}", request.Amount, request.Currency);

        var title = "Thanh toán thành công";
        var message = $"Biển số: {request.PlateNumber ?? "N/A"}. Đã thanh toán: {formattedAmount} qua {request.PaymentMethod}. Vui lòng cho xe rời bãi trước {exitDeadline} (ân hạn 15 phút).";

        var notificationPayload = JsonSerializer.Serialize(new
        {
            paymentOrderId = request.PaymentOrderId,
            amount = request.Amount,
            currency = request.Currency,
            paymentMethod = request.PaymentMethod,
            plateNumber = request.PlateNumber,
            occurredAt = request.OccurredAt,
            exitGraceCutoffAt = graceCutoffUtc
        });

        var notification = DurableNotification.Create(
            originatingEvent: "PaymentSucceededV1",
            idempotencyKey: idempotencyKey,
            recipientId: recipientId,
            channel: NotificationChannel.IN_APP,
            templateCode: "PAYMENT_SUCCESS",
            payload: notificationPayload,
            maxAttempts: 3,
            scheduledAt: null,
            title: title,
            message: message);

        await _notificationRepository.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created DurableNotification {NotificationId} for recipient {RecipientId}.", notification.Id, recipientId);

        try
        {
            return await _dispatcher.DispatchAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatch failed for notification {NotificationId}. Recording failure independently.", notification.Id);
            notification.RecordFailure(ex.Message);
            await _notificationRepository.UpdateAsync(notification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return NotificationDeliveryResult.Failed(notification.Id, notification.Attempts, ex.Message);
        }
    }
}
