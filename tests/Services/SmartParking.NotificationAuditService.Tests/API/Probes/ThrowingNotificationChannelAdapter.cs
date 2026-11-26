using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

internal sealed class ThrowingNotificationChannelAdapter(
    NotificationChannel channel,
    Func<CancellationToken, Exception> exceptionFactory) : INotificationChannelAdapter
{
    public NotificationChannel Channel { get; } = channel;

    public Task<NotificationChannelResult> SendAsync(
        Guid notificationId,
        Guid recipientId,
        string? subject,
        string body,
        string payloadJson,
        CancellationToken cancellationToken = default) =>
        throw exceptionFactory(cancellationToken);
}
