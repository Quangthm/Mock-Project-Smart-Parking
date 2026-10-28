using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetNotificationById;

public class GetNotificationByIdQueryHandler
    : IRequestHandler<GetNotificationByIdQuery, NotificationDto>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<GetNotificationByIdQueryHandler> _logger;

    public GetNotificationByIdQueryHandler(
        INotificationRepository notificationRepository,
        ILogger<GetNotificationByIdQueryHandler> logger)
    {
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    public async Task<NotificationDto> Handle(
        GetNotificationByIdQuery request,
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
                "Access denied: User {UserId} attempted to view notification {NotificationId} belonging to recipient {RecipientId}.",
                request.RequestingUserId,
                notification.Id,
                notification.RecipientId);

            throw new NotificationForbiddenException("You do not have permission to view notifications belonging to another recipient.");
        }

        return NotificationDto.FromEntity(notification);
    }
}
