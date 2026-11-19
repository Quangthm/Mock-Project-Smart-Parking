using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application;

namespace SmartParking.PaymentService.Persistence.Adapters;

public sealed class DiscoveryPriceSource(PaymentDbContext db) : IDiscoveryPriceSource
{
    public async Task<IReadOnlyList<DiscoveryRevision>> Read(DiscoveryPriceQuery query, IReadOnlyDictionary<Guid, Guid> bindings, CancellationToken ct)
    {
        var tenants = bindings.Values.Distinct().ToArray();
        // One statement observes tariffs and immutable revision payloads together. Include wider-scope
        // fallback candidates even when their tariff belongs to another site in the tenant.
        var rows = await (from tariff in db.Tariffs.AsNoTracking()
                          where tariff.IsActive && tariff.DeletedAt == null && tariff.VehicleType == query.VehicleType
                          join revision in db.PolicyRevisions.Where(r => r.PolicyType == "TARIFF")
                              on tariff.Id equals revision.PolicyId into versions
                          from revision in versions.DefaultIfEmpty()
                          where query.SiteIds.Contains(tariff.SiteId) || revision != null &&
                              (revision.ScopeType == "GLOBAL" || revision.ScopeType == "TENANT" && tenants.Contains(tariff.TenantId))
                          select new DiscoveryRevision(tariff.SiteId, tariff.TenantId, tariff.Id,
                              revision == null ? 0 : revision.RevisionNumber, revision == null ? "" : revision.ScopeType,
                              revision == null ? null : revision.ScopeId, revision == null ? tariff.CreatedAt : revision.EffectiveFrom,
                              revision == null ? null : revision.EffectiveTo, revision == null ? "" : revision.PolicyValuesJson))
            .Take(4097).ToArrayAsync(ct);
        if (rows.Length > 4096) throw DiscoveryPriceFailure.InvalidPolicy();
        return rows;
    }
}
