using MediatR;
using System.Security.Cryptography;
using System.Text.Json;

namespace SmartParking.OperationService.Application.Discovery;

public sealed class ParkingSearchHandler(IDiscoveryOwners owners, IDiscoveryCursors cursors,
    TimeProvider clock, DiscoveryPolicy policy, IDiscoveryCapacityReader capacity) : IRequestHandler<SearchParking, SearchPage>
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
        catch (System.Data.Common.DbException) when (!ct.IsCancellationRequested)
        {
            if (deadline.IsCancellationRequested) throw new DiscoveryFailure(504, "DEPENDENCY_TIMEOUT");
            throw DiscoveryFailure.Unavailable();
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

        var registeredSlots = new HashSet<Guid>();
        var facilityTimes = new Dictionary<Guid, DateTimeOffset>();
        var ranked = new List<ParkingSummary>();
        foreach (var batch in candidates.Chunk(100))
        {
            ct.ThrowIfCancellationRequested();
            var requested = batch.Where(site => filter.VehicleType == "CAR" &&
                site.SupportedVehicleTypes.Contains("CAR") && !Closed(site.ServiceWindow, filter, at)).ToArray();
            var counts = await ReadAvailability(requested, filter, at, facilityTimes, registeredSlots, ct);
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
                if (counts.TryGetValue(site.SiteId, out var availability) && summary.FieldStatus.Availability.Freshness != "stale")
                    summary = summary with
                    {
                        Availability = availability,
                        SourceAsOf = summary.SourceAsOf with { Operations = availability.AsOf },
                        FieldStatus = summary.FieldStatus with { Availability = new(
                            availability.AvailableCount.HasValue ? "fresh" : availability.Reason == "stale" ? "stale" : "unknown",
                            availability.Reason) }
                    };
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
        var delivered = ranked.Select(row => AtDelivery(row, clock.GetUtcNow(), facilityTimes.GetValueOrDefault(row.SiteId)))
            .Where(row => (!filter.AvailableOnly || row.Availability.AvailableCount > 0) &&
                (filter.MinTotalVnd is null || row.Price?.AmountVnd >= filter.MinTotalVnd) &&
                (filter.MaxTotalVnd is null || row.Price?.AmountVnd <= filter.MaxTotalVnd));
        var rows = delivered.OrderBy(s => s.DistanceMeters).ThenBy(s => s.SiteId).Take(filter.Limit + 1).ToArray();
        var items = rows.Take(filter.Limit).ToArray();
        var next = rows.Length > filter.Limit ? cursors.Encode(new(hash, version!, after?.Anchor ?? at,
            items[^1].DistanceMeters, items[^1].SiteId)) : null;
        return new(items, next, clock.GetUtcNow());
    }

    private async Task<Dictionary<Guid, Availability>> ReadAvailability(DiscoverySite[] requested, SearchFilter filter,
        DateTimeOffset at, Dictionary<Guid, DateTimeOffset> facilityTimes, HashSet<Guid> registeredSlots, CancellationToken ct)
    {
        var counts = new Dictionary<Guid, Availability>();
        if (requested.Length > 0)
        {
            var evidence = await owners.Candidates(requested, filter, at, ct);
            var slotsInBatch = 0;
            if (evidence.AsOf == default || evidence.At != at || evidence.StartAt != filter.StartAt ||
                evidence.EndAt != filter.EndAt || evidence.Items?.Length != requested.Length)
                throw DiscoveryFailure.Unavailable();
            foreach (var (site, item) in requested.Zip(evidence.Items))
            {
                if (item is null || item.SiteId != site.SiteId || item.TenantId == Guid.Empty ||
                    !Guid.TryParse(site.ConfigurationVersion, out var version) || version != item.ConfigurationVersion ||
                    item.Slots is null) throw DiscoveryFailure.Unavailable();
                foreach (var slot in item.Slots)
                    if (slot is null || slot.SlotId == Guid.Empty || !registeredSlots.Add(slot.SlotId) || ++slotsInBatch > 2048 ||
                        slot.SlotUpdatedAt == default || slot.PhysicalState is not ("AVAILABLE" or "OCCUPIED" or "UNKNOWN" or "MAINTENANCE" or "UNAVAILABLE") ||
                        slot.Outcome is not ("ELIGIBLE" or "INELIGIBLE" or "DEPENDENCY_UNAVAILABLE") ||
                        slot.Outcome == "DEPENDENCY_UNAVAILABLE" && item.Complete ||
                        slot.Outcome == "ELIGIBLE" && slot.PhysicalState is not ("AVAILABLE" or "OCCUPIED"))
                        throw DiscoveryFailure.Unavailable();
                var kind = filter.StartAt is null ? "current" : "interval";
                facilityTimes.Add(site.SiteId, evidence.AsOf);
                DateTimeOffset? stamp = null;
                int? count = null;
                if (item.Complete && Fresh(evidence.AsOf, clock.GetUtcNow()))
                {
                    var eligible = item.Slots.Where(s => s.Outcome == "ELIGIBLE").ToArray();
                    var read = await capacity.ReadCarsAsync(item.TenantId, site.SiteId, eligible.Select(s => s.SlotId).ToArray(),
                        filter.StartAt is {} start ? new(start, filter.EndAt!.Value) : null, ct,
                        eligible.Where(s => s.PhysicalState == "OCCUPIED").Select(s => s.SlotId).ToHashSet());
                    if (read.AsOf == default) throw DiscoveryFailure.Unavailable();
                    stamp = read.AsOf;
                    count = read.AvailableCount;
                }
                var fresh = Fresh(evidence.AsOf, clock.GetUtcNow()) && (stamp == null || Fresh(stamp.Value, clock.GetUtcNow()));
                counts.Add(site.SiteId, new(kind, fresh ? count : null, stamp,
                    !fresh ? "stale" : count is null ? "unknown" : null));
            }
        }
        return counts;
    }

    private bool Closed(FacilityWindow? window, SearchFilter f, DateTimeOffset at) =>
        window?.State == "closed" && Fresh(window.AsOf, clock.GetUtcNow()) &&
            (f.StartAt is null ? window.From <= at && at < window.To : window.From < f.EndAt && f.StartAt < window.To);

    private bool Fresh(DateTimeOffset stamp, DateTimeOffset now) =>
        stamp != default && now - stamp <= policy.MaxAge && stamp - now <= policy.Skew;

    private ParkingSummary AtDelivery(ParkingSummary row, DateTimeOffset now, DateTimeOffset candidateTime)
    {
        if (row.Price is not null && !Fresh(row.Price.AsOf, now))
            row = row with { Price = null, FieldStatus = row.FieldStatus with { Price = new("stale", "stale") } };
        if (row.Availability.AvailableCount is not null && row.Availability.AsOf is { } stamp &&
            (!Fresh(stamp, now) || candidateTime != default && !Fresh(candidateTime, now)))
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
        var closed = Closed(window, f, at);
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
