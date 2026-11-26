using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Models;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Tests.TestDoubles;

public class FakeCentralAuditProjectionRepository : ICentralAuditProjectionRepository
{
    public readonly List<CentralAuditProjection> Projections = new();

    public Task<CentralAuditProjection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Projections.FirstOrDefault(p => p.Id == id));
    }

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

    public Task<(IReadOnlyList<CentralAuditProjection> Items, int TotalCount)> SearchAsync(
        AuditProjectionSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<CentralAuditProjection> query = Projections;

        if (criteria.ActorId.HasValue)
            query = query.Where(a => a.ActorId == criteria.ActorId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.ActorRole))
            query = query.Where(a => string.Equals(a.ActorRole, criteria.ActorRole, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(criteria.Action))
            query = query.Where(a => a.Action.Contains(criteria.Action, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(criteria.OriginatingService))
            query = query.Where(a => string.Equals(a.OriginatingService, criteria.OriginatingService, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(criteria.ResourceType))
            query = query.Where(a => string.Equals(a.ResourceType, criteria.ResourceType, StringComparison.OrdinalIgnoreCase));

        if (criteria.ResourceId.HasValue)
            query = query.Where(a => a.ResourceId == criteria.ResourceId.Value);

        if (criteria.LotId.HasValue)
        {
            var lotText = criteria.LotId.Value.ToString();
            query = query.Where(a =>
                a.ResourceId == criteria.LotId.Value ||
                a.Details.Contains(lotText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search;
            query = query.Where(a =>
                a.Action.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.OriginatingService.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.ActorRole.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.ResourceType.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.Details.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (criteria.From.HasValue)
            query = query.Where(a => a.OccurredAt >= criteria.From.Value);

        if (criteria.To.HasValue)
            query = query.Where(a => a.OccurredAt <= criteria.To.Value);

        var ordered = query
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.ProjectedAt)
            .ToList();

        var paged = ordered
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<CentralAuditProjection>, int)>((paged, ordered.Count));
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
