using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;

public class InAppDeliveryAdapter : INotificationChannelAdapter
{
    private readonly ILogger<InAppDeliveryAdapter> _logger;

    public InAppDeliveryAdapter(ILogger<InAppDeliveryAdapter> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.IN_APP;

    public Task<NotificationChannelResult> SendAsync(
        Guid notificationId,
        Guid recipientId,
        string? subject,
        string body,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[IN_APP_FIXTURE] In-app notification {NotificationId} projected for recipient {RecipientId}. Subject: {Subject}",
            notificationId,
            recipientId,
            subject ?? "(None)");

        return Task.FromResult(new NotificationChannelResult(
            Success: true,
            DeliveryProvider: "IN_APP_FIXTURE"));
    }
}
