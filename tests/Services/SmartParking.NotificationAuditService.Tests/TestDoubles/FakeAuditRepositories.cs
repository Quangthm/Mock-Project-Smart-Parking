using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Tests.TestDoubles;

public class FakeCentralAuditProjectionRepository : ICentralAuditProjectionRepository
{
    public readonly List<CentralAuditProjection> Projections = new();

    public Task<CentralAuditProjection?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Projections.FirstOrDefault(p => p.EventId == eventId));
    }

    public Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Projections.Any(p => p.EventId == eventId));
    }

    public Task AddAsync(CentralAuditProjection projection, CancellationToken cancellationToken = default)
    {
        Projections.Add(projection);
        return Task.CompletedTask;
    }
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}
