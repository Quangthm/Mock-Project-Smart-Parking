using MediatR;
using System.Security.Cryptography;
using System.Text.Json;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Discovery;

public sealed record SearchFilter(double Lat, double Lon, string VehicleType, double RadiusKm, string? LotType,
    bool AvailableOnly, DateTimeOffset? StartAt, DateTimeOffset? EndAt, string TimeZone, long? MinTotalVnd,
    long? MaxTotalVnd, int Limit);
public sealed record SearchParking(SearchFilter Filter, string? Cursor) : IRequest<SearchPage>;
public sealed record DiscoveryCursor(string FilterHash, string CatalogueVersion, DateTimeOffset Anchor,
    double Distance, Guid SiteId);
public interface IDiscoveryCursors
{
    string Encode(DiscoveryCursor cursor);
    DiscoveryCursor Decode(string cursor);
}
public sealed record FacilityWindow(DateTimeOffset From, DateTimeOffset To, string State, DateTimeOffset AsOf);
public sealed record DiscoverySite(Guid SiteId, string Name, string Address, double Latitude, double Longitude,
    string LotType, string[] SupportedVehicleTypes, string ConfigurationVersion, DateTimeOffset AsOf, FacilityWindow? ServiceWindow = null);
public sealed record FacilityPage(DiscoverySite[] Items, string? NextCursor, string CatalogueVersion, DateTimeOffset AsOf);
public sealed record DiscoveryPrice(string Kind, long AmountVnd, string BillingUnit, int PolicyVersion, DateTimeOffset AsOf);
public sealed record SitePrice(Guid SiteId, DiscoveryPrice? Price, string? Reason);
public sealed record PriceBatch(SitePrice[] Items, DateTimeOffset AsOf);
public interface IDiscoveryOwners
{
    Task<FacilityPage> Sites(SearchFilter filter, string? cursor, CancellationToken ct);
    Task<PriceBatch> Prices(Guid[] ids, SearchFilter filter, DateTimeOffset at, CancellationToken ct);
}

public sealed record Availability(string Kind, int? AvailableCount, DateTimeOffset? AsOf, string? Reason);
public sealed record FieldState(string Freshness, string? Reason);
public sealed record SourceTimes(DateTimeOffset Facility, DateTimeOffset? Operations, DateTimeOffset Payment);
public sealed record FieldStates(FieldState Facility, FieldState Availability, FieldState Price);
public sealed record ParkingSummary(Guid SiteId, string Name, string Address, double Latitude, double Longitude,
    string LotType, string[] SupportedVehicleTypes, double DistanceMeters, Availability Availability,
    DiscoveryPrice? Price, SourceTimes SourceAsOf, FieldStates FieldStatus);
public sealed record SearchPage(ParkingSummary[] Items, string? NextCursor, DateTimeOffset AsOf);
public sealed record DiscoveryPolicy(bool Enabled, TimeSpan MaxAge, TimeSpan Skew);
public sealed class DiscoveryFailure(int status, string code, int? retryAfter = null) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public int? RetryAfter { get; } = retryAfter;
    public static DiscoveryFailure Invalid() => new(400, "VALIDATION_FAILED");
    public static DiscoveryFailure Unavailable() => new(503, "DEPENDENCY_UNAVAILABLE");
    public static DiscoveryFailure Changed() => new(409, "SEARCH_CHANGED");
}
public sealed class ParkingSearchHandler(IDiscoveryOwners owners, IDiscoveryCursors cursors,
    TimeProvider clock, DiscoveryPolicy policy, IDiscoveryCapacitySource capacity) : IRequestHandler<SearchParking, SearchPage>
{
    public async Task<SearchPage> Handle(SearchParking request, CancellationToken ct)
    {
        var filter = request.Filter;
        var at = clock.GetUtcNow();
        Validate(filter, at);
        if (!policy.Enabled) throw DiscoveryFailure.Unavailable();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(filter)));
        var after = request.Cursor is null ? null : cursors.Decode(request.Cursor);
        if (after is not null)
        {
            if (after.FilterHash != hash) throw new DiscoveryFailure(400, "INVALID_CURSOR");
            if (after.Anchor > at + policy.Skew || after.Anchor == default || !double.IsFinite(after.Distance) ||
                after.Distance < 0 || after.SiteId == Guid.Empty || string.IsNullOrWhiteSpace(after.CatalogueVersion))
                throw new DiscoveryFailure(400, "INVALID_CURSOR");
            if (at >= after.Anchor.AddMinutes(5)) throw new DiscoveryFailure(409, "CURSOR_EXPIRED");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            return await Assemble(filter, hash, after, at, deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DiscoveryFailure(504, "DEPENDENCY_TIMEOUT");
        }
    }

    private async Task<SearchPage> Assemble(SearchFilter filter, string hash, DiscoveryCursor? after,
        DateTimeOffset at, CancellationToken ct)
    {
        var candidates = new List<DiscoverySite>();
        var continuations = new HashSet<string>();
        string? cursor = null, version = after?.CatalogueVersion;
        Guid? previous = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            var page = await owners.Sites(filter, cursor, ct);
            if (!Fresh(page.AsOf, clock.GetUtcNow()) || string.IsNullOrWhiteSpace(page.CatalogueVersion)
                || page.CatalogueVersion.Length > 256 || page.Items is null || page.Items.Length > 100)
                throw DiscoveryFailure.Unavailable();
            version ??= page.CatalogueVersion;
            if (version != page.CatalogueVersion) throw DiscoveryFailure.Changed();
            foreach (var site in page.Items)
            {
                CheckSite(site, clock.GetUtcNow());
                if (previous is Guid last && site.SiteId.CompareTo(last) <= 0) throw DiscoveryFailure.Unavailable();
                previous = site.SiteId;
                candidates.Add(site);
            }
            cursor = page.NextCursor;
            if (candidates.Count > 1000 || cursor is not null &&
                (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 2048 || !continuations.Add(cursor)
                 || page.Items.Length == 0 || candidates.Count >= 1000))
                throw DiscoveryFailure.Unavailable();
        } while (cursor is not null);

        var ranked = new List<ParkingSummary>();
        foreach (var batch in candidates.Chunk(100))
        {
            ct.ThrowIfCancellationRequested();
            var prices = await owners.Prices(batch.Select(s => s.SiteId).ToArray(), filter, at, ct);
            if (prices.AsOf == default || prices.Items is null || prices.Items.Length != batch.Length)
                throw DiscoveryFailure.Unavailable();
            var byId = new Dictionary<Guid, SitePrice>();
            foreach (var item in prices.Items)
                if (item is null || !byId.TryAdd(item.SiteId, item)) throw DiscoveryFailure.Unavailable();
            foreach (var site in batch)
            {
                if (!byId.TryGetValue(site.SiteId, out var row)) throw DiscoveryFailure.Unavailable();
                var summary = Project(site, row, prices.AsOf, filter, at);
                if (summary.Availability.Reason != "closed")
                {
                    var evidence = await capacity.Read(site, filter, at, ct);
                    CapacityResult result;
                    try
                    {
                        result = DiscoveryCapacity.Evaluate(site.SiteId, filter.VehicleType, site.ConfigurationVersion,
                            filter.StartAt ?? at, filter.EndAt ?? at.AddTicks(1), clock.GetUtcNow(),
                            policy.MaxAge, policy.Skew, evidence, ct);
                    }
                    catch (InvalidCapacityEvidenceException) { throw DiscoveryFailure.Unavailable(); }
                    // Stale Facility restrictions cannot be refreshed by local count evidence.
                    if (summary.FieldStatus.Availability.Freshness != "stale")
                        summary = summary with
                        {
                            Availability = new(summary.Availability.Kind, result.Count, result.AsOf, result.Reason),
                            SourceAsOf = summary.SourceAsOf with { Operations = result.AsOf },
                            FieldStatus = summary.FieldStatus with
                            {
                                Availability = new(result.Count is not null ? "fresh" : result.Reason == "stale" ? "stale" : "unknown",
                                    result.Count is not null ? null : result.Reason)
                            }
                        };
                }
                if (!site.SupportedVehicleTypes.Contains(filter.VehicleType) ||
                    filter.LotType is not null && filter.LotType != site.LotType ||
                    summary.DistanceMeters > filter.RadiusKm * 1000 ||
                    filter.AvailableOnly && summary.Availability.AvailableCount.GetValueOrDefault() <= 0 ||
                    filter.MinTotalVnd is long min && !(summary.Price?.AmountVnd >= min) ||
                    filter.MaxTotalVnd is long max && !(summary.Price?.AmountVnd <= max)) continue;
                if (after is not null && (summary.DistanceMeters < after.Distance ||
                    summary.DistanceMeters == after.Distance && summary.SiteId.CompareTo(after.SiteId) <= 0)) continue;
                ranked.Add(summary);
            }
        }
        ct.ThrowIfCancellationRequested();
        foreach (var site in candidates) CheckSite(site, clock.GetUtcNow());
        var delivered = ranked.Select(row => AtDelivery(row, clock.GetUtcNow()))
            .Where(row => (!filter.AvailableOnly || row.Availability.AvailableCount > 0) &&
                (filter.MinTotalVnd is null || row.Price?.AmountVnd >= filter.MinTotalVnd) &&
                (filter.MaxTotalVnd is null || row.Price?.AmountVnd <= filter.MaxTotalVnd));
        var rows = delivered.OrderBy(s => s.DistanceMeters).ThenBy(s => s.SiteId).Take(filter.Limit + 1).ToArray();
        var items = rows.Take(filter.Limit).ToArray();
        var next = rows.Length > filter.Limit ? cursors.Encode(new(hash, version!, after?.Anchor ?? at,
            items[^1].DistanceMeters, items[^1].SiteId)) : null;
        return new(items, next, clock.GetUtcNow());
    }

    private bool Fresh(DateTimeOffset stamp, DateTimeOffset now) =>
        stamp != default && now - stamp <= policy.MaxAge && stamp - now <= policy.Skew;

    private ParkingSummary AtDelivery(ParkingSummary row, DateTimeOffset now)
    {
        if (row.Price is not null && !Fresh(row.Price.AsOf, now))
            row = row with { Price = null, FieldStatus = row.FieldStatus with { Price = new("stale", "stale") } };
        if (row.Availability.AvailableCount is not null && row.Availability.AsOf is { } stamp && !Fresh(stamp, now))
            row = row with
            {
                Availability = row.Availability with { AvailableCount = null, Reason = "stale" },
                FieldStatus = row.FieldStatus with { Availability = new("stale", "stale") }
            };
        return row;
    }

    private void CheckSite(DiscoverySite? site, DateTimeOffset now)
    {
        if (site is null || site.SiteId == Guid.Empty || string.IsNullOrWhiteSpace(site.Name) || site.Name.Length > 255
            || string.IsNullOrWhiteSpace(site.Address) || site.Address.Length > 2000 || !Coordinates(site.Latitude, site.Longitude)
            || site.LotType is not ("outdoor" or "basement" or "multi_storey")
            || site.SupportedVehicleTypes is not { Length: > 0 and <= 2 } categories
            || categories.Any(t => t is not ("CAR" or "MOTORCYCLE")) || categories.Distinct().Count() != categories.Length
            || string.IsNullOrWhiteSpace(site.ConfigurationVersion) || site.ConfigurationVersion.Length > 256
            || !Fresh(site.AsOf, now)) throw DiscoveryFailure.Unavailable();
        if (site.ServiceWindow is { } w && (w.From == default || w.To <= w.From || w.AsOf == default ||
            w.State is not ("open" or "closed"))) throw DiscoveryFailure.Unavailable();
    }

    private ParkingSummary Project(DiscoverySite site, SitePrice row, DateTimeOffset batchAsOf, SearchFilter f, DateTimeOffset at)
    {
        var now = clock.GetUtcNow();
        CheckSite(site, now);
        var price = row.Price;
        if (price is not null && (row.Reason is not null || price.Kind != (f.StartAt is null ? "tariff" : "estimate")
            || price.AmountVnd < 0 || price.PolicyVersion < 1 || price.AsOf == default ||
            price.BillingUnit != (f.StartAt is null ? "hour" : "total"))
            || price is null && row.Reason is not ("not_configured" or "not_supported")) throw DiscoveryFailure.Unavailable();
        var priceTime = price?.AsOf ?? batchAsOf;
        var priceStatus = !Fresh(priceTime, now) || !Fresh(batchAsOf, now) ? new FieldState("stale", "stale")
            : price is null ? new("unknown", row.Reason) : new("fresh", null);
        var window = site.ServiceWindow;
        var closed = window?.State == "closed" && Fresh(window.AsOf, now) &&
            (f.StartAt is null ? window.From <= at && at < window.To : window.From < f.EndAt && f.StartAt < window.To);
        var availabilityState = closed ? new FieldState("fresh", null)
            : window is not null && !Fresh(window.AsOf, now) ? new("stale", "stale") : new("unknown", "unknown");
        return new(site.SiteId, site.Name, site.Address, site.Latitude, site.Longitude, site.LotType,
            site.SupportedVehicleTypes, Distance(f.Lat, f.Lon, site.Latitude, site.Longitude),
            new(f.StartAt is null ? "current" : "interval", closed ? 0 : null, closed ? window!.AsOf : null,
                closed ? "closed" : availabilityState.Reason), priceStatus.Freshness == "fresh" ? price : null,
            new(site.AsOf, closed ? window!.AsOf : null, priceTime), new(new("fresh", null), availabilityState, priceStatus));
    }

    private static bool Coordinates(double lat, double lon) =>
        double.IsFinite(lat) && lat is >= -90 and <= 90 && double.IsFinite(lon) && lon is >= -180 and <= 180;
    private static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        const double rad = Math.PI / 180;
        var dy = (lat2 - lat1) * rad;
        var dx = Math.IEEERemainder(lon2 - lon1, 360) * rad;
        var a = Math.Pow(Math.Sin(dy / 2), 2) + Math.Cos(lat1 * rad) * Math.Cos(lat2 * rad) * Math.Pow(Math.Sin(dx / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    private static void Validate(SearchFilter f, DateTimeOffset now)
    {
        if (!Coordinates(f.Lat, f.Lon) || !double.IsFinite(f.RadiusKm) || f.RadiusKm is <= 0 or > 50 ||
            f.VehicleType is not ("CAR" or "MOTORCYCLE") || f.LotType is not (null or "outdoor" or "basement" or "multi_storey") ||
            f.TimeZone != "Asia/Ho_Chi_Minh" || f.Limit is < 1 or > 50 ||
            f.StartAt.HasValue != f.EndAt.HasValue || f.StartAt.HasValue &&
                (f.EndAt <= f.StartAt || f.EndAt - f.StartAt > TimeSpan.FromDays(1) || f.StartAt - now > TimeSpan.FromDays(7)) ||
            f.MinTotalVnd is < 0 || f.MaxTotalVnd is < 0 || f.MinTotalVnd > f.MaxTotalVnd ||
            f.StartAt is null && (f.MinTotalVnd is not null || f.MaxTotalVnd is not null))
            throw DiscoveryFailure.Invalid();
    }
}
