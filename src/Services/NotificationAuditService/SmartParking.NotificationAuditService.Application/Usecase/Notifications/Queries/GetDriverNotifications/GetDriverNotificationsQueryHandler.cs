using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetDriverNotifications;

public class GetDriverNotificationsQueryHandler
    : IRequestHandler<GetDriverNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<GetDriverNotificationsQueryHandler> _logger;

    public GetDriverNotificationsQueryHandler(
        INotificationRepository notificationRepository,
        ILogger<GetDriverNotificationsQueryHandler> logger)
    {
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationDto>> Handle(
        GetDriverNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Fetching notifications for recipient {RecipientId}. Status filter: {Status}, Limit: {Limit}",
            request.RecipientId,
            request.Status,
            request.Limit);

        var notifications = await _notificationRepository.GetByRecipientIdAsync(request.RecipientId, cancellationToken);

        if (request.Status.HasValue)
        {
            notifications = notifications.Where(n => n.Status == request.Status.Value).ToList();
        }

        return notifications
            .Take(request.Limit)
            .Select(NotificationDto.FromEntity)
            .ToList();
    }
}
