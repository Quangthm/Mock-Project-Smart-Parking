using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;

public interface INotificationDispatcher
{
    Task<NotificationDeliveryResult> DispatchAsync(
        DurableNotification notification,
        CancellationToken cancellationToken = default);
}
