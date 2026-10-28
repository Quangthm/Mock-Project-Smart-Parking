using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;

public class LocalSmsFixtureAdapter : INotificationChannelAdapter
{
    private readonly ILogger<LocalSmsFixtureAdapter> _logger;

    public LocalSmsFixtureAdapter(ILogger<LocalSmsFixtureAdapter> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.SMS;

    public Task<NotificationChannelResult> SendAsync(
        Guid notificationId,
        Guid recipientId,
        string? subject,
        string body,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[LOCAL_SMS_FIXTURE] Captured SMS delivery for notification {NotificationId}, recipient {RecipientId}.",
            notificationId,
            recipientId);

        return Task.FromResult(new NotificationChannelResult(
            Success: true,
            DeliveryProvider: "LOCAL_SMS_FIXTURE"));
    }
}
