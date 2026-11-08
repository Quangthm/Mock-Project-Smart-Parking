using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed record ParkingSearch(decimal Latitude, decimal Longitude, string VehicleType, decimal RadiusKm,
    bool AvailableOnly, string? LotType, DateTimeOffset? StartAt, DateTimeOffset? EndAt, long? MinTotalVnd, long? MaxTotalVnd)
{
    public override string ToString() => "[SEARCH PARAMETERS REDACTED]";

    public static bool IsRequest(string text) => Regex.IsMatch(text,
        @"^(?:find parking|search parking|parking nearby|tim bai(?: do| dau)? xe|tim bai)(?:\s|$)", RegexOptions.CultureInvariant);

    // Parse only the local allowlist. Business filtering and ordering belong to Operations.
    public static ParkingSearch? Parse(string text)
    {
        var match = Regex.Match(text, @"^(?:find parking|search parking|tim bai(?: do| dau)? xe|tim bai)\s+(car|motorcycle|o to|xe may)\s+(?:near|gan)\s+([+-]?\d{1,3}(?:\.\d{1,8})?)\s*,\s*([+-]?\d{1,3}(?:\.\d{1,8})?)(?:\s+(.*))?$");
        if (!match.Success || !Number(match.Groups[2].Value, out var lat) || lat is < -90 or > 90 ||
            !Number(match.Groups[3].Value, out var lon) || lon is < -180 or > 180) return null;
        var type = match.Groups[1].Value is "car" or "o to" ? "CAR" : "MOTORCYCLE";
        decimal radius = 5; bool available = false; string? lot = null;
        DateTimeOffset? start = null, end = null; long? min = null, max = null;
        var seen = new HashSet<string>();
        foreach (var option in match.Groups[4].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = option.Split('=', 2);
            if (pair.Length != 2 || !seen.Add(pair[0])) return null;
            var value = pair[1];
            switch (pair[0])
            {
                case "radius" when Number(value, out radius) && radius is > 0 and <= 50: break;
                case "available" when bool.TryParse(value, out available): break;
                case "lot" when value is "outdoor" or "basement" or "multi_storey": lot = value; break;
                case "from" when Instant(value, out var from): start = from; break;
                case "to" when Instant(value, out var to): end = to; break;
                case "min" when Money(value, out var floor): min = floor; break;
                case "max" when Money(value, out var ceiling): max = ceiling; break;
                default: return null;
            }
        }
        if (start.HasValue != end.HasValue || end <= start || min > max ||
            (min.HasValue || max.HasValue) && !start.HasValue) return null;
        return new(lat, lon, type, radius, available, lot, start, end, min, max);
    }

    private static bool Number(string value, out decimal result) => decimal.TryParse(value,
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);
    private static bool Money(string value, out long result) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    private static bool Instant(string value, out DateTimeOffset result)
    {
        result = default;
        return Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}t\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:z|[+-]\d{2}:\d{2})$") &&
            DateTimeOffset.TryParse(value.ToUpperInvariant(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}

public sealed record SearchField(string Freshness, string? Reason);
public sealed record SearchFields(SearchField Facility, SearchField Availability, SearchField Price);
public sealed record SearchTimes(DateTimeOffset? Facility, DateTimeOffset? Operations, DateTimeOffset? Payment);
public sealed record ParkingAvailability(string Kind, int? AvailableCount, DateTimeOffset? AsOf, string? Reason);
public sealed record ParkingPrice(string Kind, long AmountVnd, string BillingUnit, int PolicyVersion, DateTimeOffset AsOf);
public sealed record ParkingSummary(Guid SiteId, string Name, string Address, decimal Latitude, decimal Longitude,
    double DistanceMeters, string LotType, string[] SupportedVehicleTypes, ParkingAvailability Availability,
    ParkingPrice? Price, SearchTimes SourceAsOf, SearchFields FieldStatus);
public sealed record ParkingSearchPage(ParkingSummary[] Items, string? NextCursor, DateTimeOffset AsOf);
public sealed record ParkingCard(string Type, ParkingSummary Parking, DateTimeOffset AsOf);
public sealed record ParkingPageCard(string Type, string? NextCursor, Guid SearchMessageId, DateTimeOffset AsOf);
