using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Tests.TestDoubles;

public class FakeNotificationRepository : INotificationRepository
{
    public readonly List<DurableNotification> Notifications = new();

    public Task<DurableNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Notifications.FirstOrDefault(n => n.Id == id));
    }

    public Task<DurableNotification?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Notifications.FirstOrDefault(n => n.IdempotencyKey == idempotencyKey));
    }

    public Task<IReadOnlyList<DurableNotification>> GetByRecipientIdAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        var list = Notifications
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<DurableNotification>>(list);
    }

    public Task<IReadOnlyList<DurableNotification>> GetPendingDispatchAsync(int limit, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var list = Notifications
            .Where(n => (n.Status == NotificationStatus.PENDING || n.Status == NotificationStatus.PROCESSING) && n.ScheduledAt <= now)
            .OrderBy(n => n.ScheduledAt)
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<DurableNotification>>(list);
    }

    public Task AddAsync(DurableNotification notification, CancellationToken cancellationToken = default)
    {
        Notifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DurableNotification notification, CancellationToken cancellationToken = default)
    {
        var index = Notifications.FindIndex(n => n.Id == notification.Id);
        if (index >= 0)
        {
            Notifications[index] = notification;
        }
        else
        {
            Notifications.Add(notification);
        }
        return Task.CompletedTask;
    }
}

public class FakeNotificationTemplateRepository : INotificationTemplateRepository
{
    public readonly List<NotificationTemplate> Templates = new();

    public Task<NotificationTemplate?> GetByCodeAndVersionAsync(string templateCode, int version, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Templates.FirstOrDefault(t => t.TemplateCode == templateCode && t.Version == version));
    }

    public Task<NotificationTemplate?> GetActiveByCodeAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Templates.FirstOrDefault(t => t.TemplateCode == templateCode && t.IsActive));
    }

    public Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        Templates.Add(template);
        return Task.CompletedTask;
    }
}

public class FakeNotificationDispatcher : INotificationDispatcher
{
    public bool ShouldFail { get; set; } = false;
    public string FailureMessage { get; set; } = "Simulated delivery adapter error";
    public int DispatchCalls { get; private set; }

    public Task<NotificationDeliveryResult> DispatchAsync(DurableNotification notification, CancellationToken cancellationToken = default)
    {
        DispatchCalls++;
        notification.MarkProcessing();

        if (ShouldFail)
        {
            notification.RecordFailure(FailureMessage);
            return Task.FromResult(NotificationDeliveryResult.Failed(notification.Id, notification.Attempts, FailureMessage));
        }

        notification.MarkDelivered();
        return Task.FromResult(NotificationDeliveryResult.Delivered(notification.Id, notification.Attempts));
    }
}
