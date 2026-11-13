using MediatR;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application;

public sealed record DiscoveryPriceQuery(Guid[] SiteIds, string VehicleType, DateTimeOffset At,
    DateTimeOffset? StartAt = null, DateTimeOffset? EndAt = null) : IRequest<DiscoveryPriceBatch>;
public sealed record DiscoveryPrice(string Kind, long AmountVnd, string BillingUnit, int PolicyVersion, DateTimeOffset AsOf);
public sealed record DiscoveryPriceItem(Guid SiteId, DiscoveryPrice? Price, string? Reason);
public sealed record DiscoveryPriceBatch(DiscoveryPriceItem[] Items, DateTimeOffset AsOf);
public sealed record DiscoveryRevision(Guid SiteId, Guid TenantId, Guid TariffId, int Revision, string Scope,
    Guid? ScopeId, DateTimeOffset From, DateTimeOffset? To, string PolicyJson);

public interface IDiscoveryPriceSource
{
    Task<IReadOnlyList<DiscoveryRevision>> Read(DiscoveryPriceQuery query, IReadOnlyDictionary<Guid, Guid> bindings, CancellationToken ct);
}

public interface IDiscoverySiteBindings
{
    IReadOnlyDictionary<Guid, Guid> Require(Guid[] siteIds);
}

public sealed class DiscoveryPriceFailure(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public static DiscoveryPriceFailure InvalidPolicy() => new(503, "DEPENDENCY_UNAVAILABLE");
}

public sealed class DiscoveryPriceHandler(IDiscoveryPriceSource source, IDiscoverySiteBindings sites, TimeProvider clock,
    IParkingPricingCalculator calculator) : IRequestHandler<DiscoveryPriceQuery, DiscoveryPriceBatch>
{
    public async Task<DiscoveryPriceBatch> Handle(DiscoveryPriceQuery query, CancellationToken ct)
    {
        if (query.SiteIds is not { Length: > 0 and <= 100 } || query.SiteIds.Contains(Guid.Empty)
            || query.SiteIds.Distinct().Count() != query.SiteIds.Length || query.VehicleType is not ("CAR" or "MOTORCYCLE")
            || query.At == default || query.StartAt.HasValue != query.EndAt.HasValue || query.EndAt <= query.StartAt
            || query.At.Year is < 2 or > 9998 || query.StartAt?.Year is < 2 or > 9998 || query.EndAt?.Year is < 2 or > 9998)
            throw new DiscoveryPriceFailure(400, "VALIDATION_FAILED");

        var now = clock.GetUtcNow();
        if (query.StartAt is { } start && (query.EndAt - start > TimeSpan.FromHours(24) || start > now.AddDays(7)))
            throw new DiscoveryPriceFailure(400, "VALIDATION_FAILED");
        var bindings = sites.Require(query.SiteIds);
        var facts = await source.Read(query, bindings, ct);
        var observedAt = clock.GetUtcNow();
        var items = new List<DiscoveryPriceItem>(query.SiteIds.Length);
        foreach (var site in query.SiteIds)
        {
            ct.ThrowIfCancellationRequested();
            var instant = query.StartAt ?? query.At;
            var forSite = facts.Where(r => r.SiteId == site).ToArray();
            if (forSite.Any(r => r.TenantId != bindings[site] || r.Revision < 1 || r.To <= r.From))
                throw DiscoveryPriceFailure.InvalidPolicy();
            var relevant = facts.Where(r => r.SiteId == site || r.Scope == "GLOBAL"
                || r.Scope == "TENANT" && r.TenantId == bindings[site])
                .Where(r => query.EndAt is { } end ? r.From < end : r.From <= instant)
                .Where(r => r.To == null || r.To > instant).ToArray();
            if (relevant.Length == 0) { items.Add(new(site, null, "not_configured")); continue; }
            if (relevant.Any(r => r.Scope is not ("SITE" or "ZONE" or "TENANT" or "GLOBAL")
                || r.Scope == "SITE" && r.ScopeId != site))
                throw DiscoveryPriceFailure.InvalidPolicy();
            if (relevant.Any(r => r.Scope != "SITE")) { items.Add(new(site, null, "not_supported")); continue; }
            var ordered = relevant.OrderBy(r => r.From).ToArray();
            for (int i = 1; i < ordered.Length; i++)
                if (ordered[i - 1].To == null || ordered[i - 1].To > ordered[i].From)
                    throw DiscoveryPriceFailure.InvalidPolicy();
            var revision = ordered[0];
            if (ordered.Length != 1 || revision.From > instant || query.EndAt > revision.To)
            { items.Add(new(site, null, "not_supported")); continue; }
            var rule = DiscoverySchedule.Read(revision.PolicyJson);
            long amount;
            bool interval = query.StartAt.HasValue;
            if (interval)
            {
                // An unversioned walk-in grace column does not describe discovery terms.
                if (rule.DiscoveryGraceMinutes == null)
                { items.Add(new(site, null, "not_supported")); continue; }
                var category = Enum.Parse<PricingVehicleType>(query.VehicleType);
                var policy = new PricingPolicySnapshot(revision.TariffId, "Discovery", revision.Revision, category,
                    rule.DiscoveryGraceMinutes.Value, rule.Blocks, rule.TimeZone, MonetaryRoundingMode.HALF_UP_THOUSAND);
                amount = calculator.Calculate(new(bindings[site], site, category, instant, query.EndAt!.Value, policy)).FinalAmountVnd;
            }
            else amount = rule.RateAt(instant);
            items.Add(new(site, new(interval ? "estimate" : "tariff", amount, interval ? "total" : "hour", revision.Revision, observedAt), null));
        }
        return new(items.ToArray(), observedAt);
    }
}
