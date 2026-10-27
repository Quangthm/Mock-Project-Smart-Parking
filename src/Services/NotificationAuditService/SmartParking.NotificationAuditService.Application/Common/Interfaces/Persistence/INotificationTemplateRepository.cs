using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByCodeAndVersionAsync(string templateCode, int version, CancellationToken cancellationToken = default);
    Task<NotificationTemplate?> GetActiveByCodeAsync(string templateCode, CancellationToken cancellationToken = default);
    Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default);
}
