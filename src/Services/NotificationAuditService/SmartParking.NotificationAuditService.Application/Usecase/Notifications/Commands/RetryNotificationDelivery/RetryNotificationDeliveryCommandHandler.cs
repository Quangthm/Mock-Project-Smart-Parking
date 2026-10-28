using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.RetryNotificationDelivery;

public class RetryNotificationDeliveryCommandHandler
    : IRequestHandler<RetryNotificationDeliveryCommand, NotificationDeliveryResult>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RetryNotificationDeliveryCommandHandler> _logger;

    public RetryNotificationDeliveryCommandHandler(
        INotificationRepository notificationRepository,
        INotificationDispatcher dispatcher,
        IUnitOfWork unitOfWork,
        ILogger<RetryNotificationDeliveryCommandHandler> logger)
    {
        _notificationRepository = notificationRepository;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotificationDeliveryResult> Handle(
        RetryNotificationDeliveryCommand request,
        CancellationToken cancellationToken)
    {
        var notification = await _notificationRepository.GetByIdAsync(request.NotificationId, cancellationToken);
        if (notification == null)
        {
            throw new KeyNotFoundException($"Notification with ID {request.NotificationId} was not found.");
        }

        // Recipient Scope Authorization
        var isAdmin = string.Equals(request.RequestingUserRole, "Admin", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && notification.RecipientId != request.RequestingUserId)
        {
            _logger.LogWarning(
                "Access denied: User {UserId} attempted to retry notification {NotificationId} belonging to recipient {RecipientId}.",
                request.RequestingUserId,
                notification.Id,
                notification.RecipientId);

            throw new NotificationForbiddenException("You do not have permission to retry notifications belonging to another recipient.");
        }

        // Idempotency check: Already delivered notifications should not be re-sent
        if (notification.Status == NotificationStatus.DELIVERED)
        {
            _logger.LogInformation(
                "Notification {NotificationId} has already been delivered. Skipping retry (Idempotent).",
                notification.Id);

            return NotificationDeliveryResult.Delivered(notification.Id, notification.Attempts);
        }

        // Reset state for retry
        notification.ResetForRetry(DateTimeOffset.UtcNow);
        await _notificationRepository.UpdateAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Retrying notification {NotificationId} delivery by user {UserId}. Current attempt: {Attempts}.",
            notification.Id,
            request.RequestingUserId,
            notification.Attempts);

        try
        {
            return await _dispatcher.DispatchAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during retry dispatch of notification {NotificationId}.",
                notification.Id);

            notification.RecordFailure(ex.Message);
            await _notificationRepository.UpdateAsync(notification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return NotificationDeliveryResult.Failed(notification.Id, notification.Attempts, ex.Message);
        }
    }
}
