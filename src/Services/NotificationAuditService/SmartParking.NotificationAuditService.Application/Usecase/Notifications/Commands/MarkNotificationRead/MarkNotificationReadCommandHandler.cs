using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkNotificationRead;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, NotificationDto?>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkNotificationReadCommandHandler> _logger;

    public MarkNotificationReadCommandHandler(
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        ILogger<MarkNotificationReadCommandHandler> logger)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotificationDto?> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notificationRepository.GetByIdAsync(request.Id, cancellationToken);
        if (notification == null)
        {
            _logger.LogWarning("Notification {NotificationId} not found.", request.Id);
            return null;
        }

        if (request.RequestingUserId.HasValue && request.RequestingUserId.Value != Guid.Empty)
        {
            if (notification.RecipientId != request.RequestingUserId.Value)
            {
                _logger.LogWarning(
                    "Access denied: User {UserId} attempted to mark notification {NotificationId} belonging to recipient {RecipientId}.",
                    request.RequestingUserId.Value,
                    notification.Id,
                    notification.RecipientId);

                throw new NotificationForbiddenException("You do not have permission to modify this notification.");
            }
        }

        notification.MarkAsRead();
        await _notificationRepository.UpdateAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Notification {NotificationId} marked as read.", notification.Id);
        return NotificationDto.FromEntity(notification);
    }
}
