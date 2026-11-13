using System.Globalization;
using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Chat;

namespace SmartParking.IoTService.Infrastructure.Assistant;

public sealed partial class OwnerReadClient
{
    private const string SearchClient = "assistant-parking-search";
    private static readonly JsonSerializerOptions SearchJson = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task<ParkingSearchPage> Search(ParkingSearch query, string? cursor, CancellationToken ct)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("lat", query.Latitude.ToString(CultureInfo.InvariantCulture)),
            new("lon", query.Longitude.ToString(CultureInfo.InvariantCulture)),
            new("vehicleType", query.VehicleType),
            new("radiusKm", query.RadiusKm.ToString(CultureInfo.InvariantCulture)),
            new("availableOnly", query.AvailableOnly ? "true" : "false"),
            new("limit", "19")
        };
        if (query.LotType is not null) parameters.Add(new("lotType", query.LotType));
        if (query.StartAt.HasValue)
        {
            parameters.Add(new("startAt", query.StartAt.Value.ToString("O")));
            parameters.Add(new("endAt", query.EndAt!.Value.ToString("O")));
        }
        if (query.MinTotalVnd.HasValue) parameters.Add(new("minTotalVnd", query.MinTotalVnd.Value.ToString(CultureInfo.InvariantCulture)));
        if (query.MaxTotalVnd.HasValue) parameters.Add(new("maxTotalVnd", query.MaxTotalVnd.Value.ToString(CultureInfo.InvariantCulture)));
        if (cursor is not null) parameters.Add(new("cursor", cursor));
        var path = "/api/parking-search?" + string.Join("&", parameters.Select(p => p.Key + "=" + Uri.EscapeDataString(p.Value)));
        var data = await Read(SearchClient, path, ct);
        try
        {
            var page = data.Deserialize<ParkingSearchPage>(SearchJson);
            if (page is null || !Fresh(page.AsOf) || page.Items is null || page.Items.Length > 19 ||
                page.NextCursor is { Length: 0 or > 2048 } || page.Items.Any(s => !ValidSite(s, query)) ||
                page.Items.Select(s => s.SiteId).Distinct().Count() != page.Items.Length ||
                !page.Items.Select(s => s.SiteId).SequenceEqual(page.Items.OrderBy(s => s.DistanceMeters).ThenBy(s => s.SiteId).Select(s => s.SiteId)))
                throw new JsonException();
            return page;
        }
        catch (JsonException) { throw new OwnerReadFailure("dependency_unavailable"); }
    }

    private bool ValidSite(ParkingSummary? site, ParkingSearch query)
    {
        if (site is null || site.SiteId == Guid.Empty || string.IsNullOrWhiteSpace(site.Name) || site.Name.Length > 255 ||
            string.IsNullOrWhiteSpace(site.Address) || site.Address.Length > 2000 || site.Latitude is < -90 or > 90 ||
            site.Longitude is < -180 or > 180 || !double.IsFinite(site.DistanceMeters) || site.DistanceMeters < 0 || site.DistanceMeters > (double)query.RadiusKm * 1000 ||
            site.SupportedVehicleTypes is null || !site.SupportedVehicleTypes.Contains(query.VehicleType) ||
            site.LotType is not ("outdoor" or "basement" or "multi_storey") ||
            query.LotType is not null && query.LotType != site.LotType ||
            site.Availability is null || site.Availability.Kind != (query.StartAt.HasValue ? "interval" : "current") ||
            site.FieldStatus?.Facility?.Freshness != "fresh" || site.SourceAsOf?.Facility is null || !Fresh(site.SourceAsOf.Facility.Value)) return false;

        var count = site.Availability.AvailableCount;
        if (count < 0 || !Consistent(site.FieldStatus.Availability, count.HasValue) ||
            (count.HasValue && (!site.Availability.AsOf.HasValue || !Fresh(site.Availability.AsOf.Value) || site.SourceAsOf.Operations != site.Availability.AsOf)) || (query.AvailableOnly && !(count > 0))) return false;
        var price = site.Price;
        return !(query.MinTotalVnd is long min && !(price?.AmountVnd >= min)) &&
            !(query.MaxTotalVnd is long max && !(price?.AmountVnd <= max)) &&
            Consistent(site.FieldStatus.Price, price is not null) &&
            (price is null || price.Kind == (query.StartAt.HasValue ? "estimate" : "tariff") &&
                price.AmountVnd >= 0 && !string.IsNullOrWhiteSpace(price.BillingUnit) &&
                price.PolicyVersion > 0 && Fresh(price.AsOf) && site.SourceAsOf.Payment == price.AsOf);
    }

    private bool Fresh(DateTimeOffset value) => value != default && options.SearchMaxAgeSeconds > 0 &&
        clock.GetUtcNow() - value <= TimeSpan.FromSeconds(options.SearchMaxAgeSeconds) &&
        value - clock.GetUtcNow() <= TimeSpan.FromSeconds(options.SearchSkewSeconds);

    private static bool Consistent(SearchField? field, bool known) => known
        ? field is { Freshness: "fresh", Reason: null }
        : field is { Freshness: "stale" or "unknown", Reason: "unknown" or "stale" or "not_configured" or "not_supported" };
}
