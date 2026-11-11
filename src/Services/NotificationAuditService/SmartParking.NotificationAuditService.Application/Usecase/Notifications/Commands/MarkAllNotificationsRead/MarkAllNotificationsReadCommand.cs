namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkAllNotificationsRead;

using MediatR;

public record MarkAllNotificationsReadCommand : IRequest<int>
{
    public Guid RecipientId { get; init; }

    public MarkAllNotificationsReadCommand() { }

    public MarkAllNotificationsReadCommand(Guid recipientId)
    {
        RecipientId = recipientId;
    }
}
