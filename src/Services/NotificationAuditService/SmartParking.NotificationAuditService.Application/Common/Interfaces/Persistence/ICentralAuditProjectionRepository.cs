using SmartParking.NotificationAuditService.Application.Common.Models;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;

public interface ICentralAuditProjectionRepository
{
    Task<CentralAuditProjection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CentralAuditProjection?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task AddAsync(CentralAuditProjection projection, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CentralAuditProjection> Items, int TotalCount)> SearchAsync(
        AuditProjectionSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
