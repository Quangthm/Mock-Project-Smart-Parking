using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetDriverNotifications;

public record GetDriverNotificationsQuery : IRequest<IReadOnlyList<NotificationDto>>
{
    public Guid RecipientId { get; init; }
    public NotificationStatus? Status { get; init; }
    public int Limit { get; init; } = 50;
}
