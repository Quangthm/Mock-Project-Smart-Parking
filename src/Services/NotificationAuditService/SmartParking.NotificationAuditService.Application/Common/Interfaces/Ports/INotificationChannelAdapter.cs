using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;

public record NotificationChannelResult(
    bool Success,
    string DeliveryProvider,
    string? ErrorMessage = null);

public interface INotificationChannelAdapter
{
    NotificationChannel Channel { get; }
    Task<NotificationChannelResult> SendAsync(
        Guid notificationId,
        Guid recipientId,
        string? subject,
        string body,
        string payloadJson,
        CancellationToken cancellationToken = default);
}
