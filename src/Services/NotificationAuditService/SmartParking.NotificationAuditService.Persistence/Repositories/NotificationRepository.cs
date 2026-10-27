using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationAuditDbContext _context;

    public NotificationRepository(NotificationAuditDbContext context)
    {
        _context = context;
    }

    public async Task<DurableNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DurableNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<DurableNotification?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.DurableNotifications
            .FirstOrDefaultAsync(n => n.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<DurableNotification>> GetByRecipientIdAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        return await _context.DurableNotifications
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DurableNotification>> GetPendingDispatchAsync(int limit, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _context.DurableNotifications
            .Where(n => (n.Status == NotificationStatus.PENDING || n.Status == NotificationStatus.PROCESSING) && n.ScheduledAt <= now)
            .OrderBy(n => n.ScheduledAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DurableNotification notification, CancellationToken cancellationToken = default)
    {
        await _context.DurableNotifications.AddAsync(notification, cancellationToken);
    }

    public Task UpdateAsync(DurableNotification notification, CancellationToken cancellationToken = default)
    {
        _context.DurableNotifications.Update(notification);
        return Task.CompletedTask;
    }
}
