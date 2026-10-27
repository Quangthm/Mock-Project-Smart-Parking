using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;

public interface ICentralAuditProjectionRepository
{
    Task<CentralAuditProjection?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task AddAsync(CentralAuditProjection projection, CancellationToken cancellationToken = default);
}
