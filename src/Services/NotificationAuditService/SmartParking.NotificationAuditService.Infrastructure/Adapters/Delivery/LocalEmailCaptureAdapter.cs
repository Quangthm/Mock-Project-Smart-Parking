using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;

public class LocalEmailCaptureAdapter : INotificationChannelAdapter
{
    private readonly ILogger<LocalEmailCaptureAdapter> _logger;

    public LocalEmailCaptureAdapter(ILogger<LocalEmailCaptureAdapter> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.EMAIL;

    public Task<NotificationChannelResult> SendAsync(
        Guid notificationId,
        Guid recipientId,
        string? subject,
        string body,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[LOCAL_EMAIL_CAPTURE_FIXTURE] Captured email delivery for notification {NotificationId}, recipient {RecipientId}. Subject: {Subject}",
            notificationId,
            recipientId,
            subject ?? "(No Subject)");

        return Task.FromResult(new NotificationChannelResult(
            Success: true,
            DeliveryProvider: "LOCAL_EMAIL_CAPTURE_FIXTURE"));
    }
}
