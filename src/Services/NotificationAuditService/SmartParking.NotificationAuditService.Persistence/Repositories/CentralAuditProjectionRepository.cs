using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Models;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Persistence.Repositories;

public class CentralAuditProjectionRepository : ICentralAuditProjectionRepository
{
    private readonly NotificationAuditDbContext _context;

    public CentralAuditProjectionRepository(NotificationAuditDbContext context)
    {
        _context = context;
    }

    public async Task<CentralAuditProjection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CentralAuditProjections
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<CentralAuditProjection?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.CentralAuditProjections
            .FirstOrDefaultAsync(a => a.EventId == eventId, cancellationToken);
    }

    public async Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.CentralAuditProjections
            .AnyAsync(a => a.EventId == eventId, cancellationToken);
    }

    public async Task AddAsync(CentralAuditProjection projection, CancellationToken cancellationToken = default)
    {
        await _context.CentralAuditProjections.AddAsync(projection, cancellationToken);
    }

    public async Task<(IReadOnlyList<CentralAuditProjection> Items, int TotalCount)> SearchAsync(
        AuditProjectionSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = BuildBaseQuery(criteria);

        // Push LotId (details jsonb) and free-text Search directly to PostgreSQL SQL when running on Npgsql.
        if (_context.Database.IsNpgsql() && (criteria.LotId.HasValue || !string.IsNullOrWhiteSpace(criteria.Search)))
        {
            query = ApplyPostgresJsonbAndSearchSql(query, criteria);
        }
        else if (criteria.LotId.HasValue || !string.IsNullOrWhiteSpace(criteria.Search))
        {
            // Non-Npgsql fallback (EF Core InMemory unit/integration test host)
            var candidates = await query
                .OrderByDescending(a => a.OccurredAt)
                .ThenByDescending(a => a.ProjectedAt)
                .ToListAsync(cancellationToken);

            var filtered = ApplyInMemoryLotAndSearch(candidates, criteria).ToList();
            var paged = filtered
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToList();

            return (paged, filtered.Count);
        }

        var orderedQuery = query
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.ProjectedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var items = await orderedQuery
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private IQueryable<CentralAuditProjection> BuildBaseQuery(AuditProjectionSearchCriteria criteria)
    {
        var query = _context.CentralAuditProjections
            .AsNoTracking()
            .AsQueryable();

        return ApplyStandardPredicates(query, criteria);
    }

    private IQueryable<CentralAuditProjection> ApplyPostgresJsonbAndSearchSql(
        IQueryable<CentralAuditProjection> _,
        AuditProjectionSearchCriteria criteria)
    {
        var sql = new StringBuilder(
            "SELECT id, originating_service, event_id, actor_id, actor_role, action, resource_type, resource_id, details, occurred_at, projected_at " +
            "FROM central_audit_projections WHERE TRUE");
        var parameters = new List<object>();

        if (criteria.LotId.HasValue)
        {
            var lotIdParam = new NpgsqlParameter("lotId", criteria.LotId.Value);
            var lotPatternParam = new NpgsqlParameter("lotPattern", $"%{EscapeLike(criteria.LotId.Value.ToString())}%");
            parameters.Add(lotIdParam);
            parameters.Add(lotPatternParam);
            sql.Append(" AND (resource_id = @lotId OR details::text ILIKE @lotPattern ESCAPE '\\')");
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var searchPatternParam = new NpgsqlParameter("searchPattern", $"%{EscapeLike(criteria.Search)}%");
            parameters.Add(searchPatternParam);
            sql.Append(
                " AND (action ILIKE @searchPattern ESCAPE '\\'" +
                " OR originating_service ILIKE @searchPattern ESCAPE '\\'" +
                " OR actor_role ILIKE @searchPattern ESCAPE '\\'" +
                " OR resource_type ILIKE @searchPattern ESCAPE '\\'" +
                " OR details::text ILIKE @searchPattern ESCAPE '\\'" +
                " OR actor_id::text ILIKE @searchPattern ESCAPE '\\'" +
                " OR resource_id::text ILIKE @searchPattern ESCAPE '\\')");
        }

        var sqlQuery = _context.CentralAuditProjections
            .FromSqlRaw(sql.ToString(), parameters.ToArray())
            .AsNoTracking();

        return ApplyStandardPredicates(sqlQuery, criteria);
    }

    private static IQueryable<CentralAuditProjection> ApplyStandardPredicates(
        IQueryable<CentralAuditProjection> query,
        AuditProjectionSearchCriteria criteria)
    {
        if (criteria.ActorId.HasValue)
        {
            query = query.Where(a => a.ActorId == criteria.ActorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ActorRole))
        {
            var role = criteria.ActorRole.ToLower();
            query = query.Where(a => a.ActorRole.ToLower() == role);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Action))
        {
            var action = criteria.Action.ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(criteria.OriginatingService))
        {
            var service = criteria.OriginatingService.ToLower();
            query = query.Where(a => a.OriginatingService.ToLower() == service);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ResourceType))
        {
            var resourceType = criteria.ResourceType.ToLower();
            query = query.Where(a => a.ResourceType.ToLower() == resourceType);
        }

        if (criteria.ResourceId.HasValue)
        {
            query = query.Where(a => a.ResourceId == criteria.ResourceId.Value);
        }

        if (criteria.From.HasValue)
        {
            query = query.Where(a => a.OccurredAt >= criteria.From.Value);
        }

        if (criteria.To.HasValue)
        {
            query = query.Where(a => a.OccurredAt <= criteria.To.Value);
        }

        return query;
    }

    private static IEnumerable<CentralAuditProjection> ApplyInMemoryLotAndSearch(
        IEnumerable<CentralAuditProjection> candidates,
        AuditProjectionSearchCriteria criteria)
    {
        var filtered = candidates;

        if (criteria.LotId.HasValue)
        {
            var lotIdText = criteria.LotId.Value.ToString();
            filtered = filtered.Where(a =>
                a.ResourceId == criteria.LotId.Value ||
                a.Details.Contains(lotIdText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search;
            filtered = filtered.Where(a =>
                a.Action.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.OriginatingService.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.ActorRole.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.ResourceType.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.Details.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.ActorId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.ResourceId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return filtered;
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
