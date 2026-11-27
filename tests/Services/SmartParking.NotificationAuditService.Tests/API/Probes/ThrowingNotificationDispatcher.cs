using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

internal sealed class ThrowingNotificationDispatcher(Func<Exception> exceptionFactory) : INotificationDispatcher
{
    public Task<NotificationDeliveryResult> DispatchAsync(
        DurableNotification notification,
        CancellationToken cancellationToken = default) =>
        throw exceptionFactory();
}
