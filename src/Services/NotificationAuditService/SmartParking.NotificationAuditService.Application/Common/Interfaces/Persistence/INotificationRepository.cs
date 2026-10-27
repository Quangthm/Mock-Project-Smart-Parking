using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;

public interface INotificationRepository
{
    Task<DurableNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DurableNotification?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DurableNotification>> GetByRecipientIdAsync(Guid recipientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DurableNotification>> GetPendingDispatchAsync(int limit, CancellationToken cancellationToken = default);
    Task AddAsync(DurableNotification notification, CancellationToken cancellationToken = default);
    Task UpdateAsync(DurableNotification notification, CancellationToken cancellationToken = default);
}
