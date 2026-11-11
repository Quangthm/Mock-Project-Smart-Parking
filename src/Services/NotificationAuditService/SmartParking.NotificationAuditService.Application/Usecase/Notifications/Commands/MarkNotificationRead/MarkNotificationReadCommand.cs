namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkNotificationRead;

using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;

public record MarkNotificationReadCommand : IRequest<NotificationDto?>
{
    public Guid Id { get; init; }
    public Guid NotificationId => Id;
    public Guid? RequestingUserId { get; init; }

    public MarkNotificationReadCommand() { }

    public MarkNotificationReadCommand(Guid id, Guid? requestingUserId = null)
    {
        Id = id;
        RequestingUserId = requestingUserId;
    }
}
