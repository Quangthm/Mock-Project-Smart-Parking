using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;

public class LocalPushFixtureAdapter : INotificationChannelAdapter
{
    private readonly ILogger<LocalPushFixtureAdapter> _logger;

    public LocalPushFixtureAdapter(ILogger<LocalPushFixtureAdapter> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.PUSH;

    public Task<NotificationChannelResult> SendAsync(
        Guid notificationId,
        Guid recipientId,
        string? subject,
        string body,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[LOCAL_PUSH_FIXTURE] Captured mobile push delivery for notification {NotificationId}, recipient {RecipientId}. Subject: {Subject}",
            notificationId,
            recipientId,
            subject ?? "(No Subject)");

        return Task.FromResult(new NotificationChannelResult(
            Success: true,
            DeliveryProvider: "LOCAL_PUSH_FIXTURE"));
    }
}
