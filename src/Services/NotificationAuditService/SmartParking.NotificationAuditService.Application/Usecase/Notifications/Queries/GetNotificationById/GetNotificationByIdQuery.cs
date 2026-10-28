using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetNotificationById;

public record GetNotificationByIdQuery : IRequest<NotificationDto>
{
    public Guid NotificationId { get; init; }
    public Guid RequestingUserId { get; init; }
    public string RequestingUserRole { get; init; } = string.Empty;
}
