using System.Globalization;
using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed record ParkingSearch(decimal Latitude, decimal Longitude, string VehicleType, decimal RadiusKm,
    bool AvailableOnly, string? LotType, DateTimeOffset? StartAt, DateTimeOffset? EndAt, long? MinTotalVnd, long? MaxTotalVnd)
{
    private const string VietnameseVerb = @"(?:giup toi |toi muon )?tim bai(?: (?:do|dau)(?: xe)?)?";
    private const string SearchVerb = $@"(?:(?:please |help me )?(?:find parking|search parking)|{VietnameseVerb})";
    private const string Vehicle = @"car|motorcycle|o to|xe may";
    private const string Coordinate = @"[+-]?\d{1,3}(?:\.\d{1,8})?";
    private const string CoordinatePair = $@"({Coordinate})\s*,\s*({Coordinate})";
    private const string Now = @"(?:now|right now|bay gio|ngay bay gio|hien tai)";

    public override string ToString() => "[SEARCH PARAMETERS REDACTED]";

    public static bool IsRequest(string text) => Regex.IsMatch(text,
        $@"^(?:{SearchVerb}|parking nearby)(?:\s|$)", RegexOptions.CultureInvariant);

    internal static bool IsVietnameseRequest(string text) => Regex.IsMatch(text, $@"^{VietnameseVerb}(?:\s|$)");

    internal static string Clarification(string text, bool english)
    {
        var coordinates = Regex.Matches(text, $@"(?<![\w.,+-]){CoordinatePair}(?![\w.,+-])");
        var vehicles = Regex.Matches(text, $@"(?:^{SearchVerb}\s+|\b(?:cho|for|hoac|or)\s+)({Vehicle})\b")
            .Select(match => match.Groups[1].Value is "car" or "o to" ? "CAR" : "MOTORCYCLE").Distinct().Count();
        var starts = Regex.Matches(text, @"(?:^| )from=(\S+)");
        var ends = Regex.Matches(text, @"(?:^| )to=(\S+)");
        var explicitTime = starts.Count == 1 && ends.Count == 1 && Instant(starts[0].Groups[1].Value, out var from) &&
            Instant(ends[0].Groups[1].Value, out var to) && to > from;
        var now = Regex.Match(text, $@" {Now}$");
        var explicitNow = starts.Count == 0 && ends.Count == 0 && now.Success &&
            !Regex.IsMatch(text[..now.Index], @"\b(?:not|khong|or|hay|hoac)$");
        var missing = new List<string>();
        if (coordinates.Count != 1 || !Location(coordinates[0].Groups[1].Value, coordinates[0].Groups[2].Value, out _, out _))
            missing.Add(english ? "one origin (latitude,longitude)" : "một điểm xuất phát (vĩ độ,kinh độ)");
        if (vehicles != 1) missing.Add(english ? "one vehicle type (car or motorcycle)" : "một loại xe (ô tô hoặc xe máy)");
        if (!explicitTime && !explicitNow) missing.Add(english ? "when (now or the exact start and end times)" : "khung giờ (tìm ngay hoặc giờ bắt đầu và kết thúc cụ thể)");
        var prompt = missing.Count == 0 ? "" :
            (english ? "Please confirm " : "Bạn cho mình biết ") + string.Join(", ", missing) + ". ";
        return prompt + (english
            ? "Resend the complete search in one message, for example ‘find parking car near 10.77,106.70’ for now. For a future search, append from= and to= with full ISO 8601 times including UTC offsets."
            : "Hãy gửi lại yêu cầu đầy đủ trong một tin nhắn, ví dụ ‘tìm bãi ô tô gần 10.77,106.70’ để tìm ngay. Nếu tìm cho khung giờ sau, thêm from= và to= với ngày giờ ISO 8601 kèm múi giờ.");
    }

    // Parse only the local allowlist. Business filtering and ordering belong to Operations.
    public static ParkingSearch? Parse(string text)
    {
        var match = Regex.Match(text, $@"^{SearchVerb}\s+({Vehicle})\s+(?:near|gan)\s+{CoordinatePair}(?:\s+(.*))?$");
        if (!match.Success || !Location(match.Groups[2].Value, match.Groups[3].Value, out var lat, out var lon)) return null;
        var type = match.Groups[1].Value is "car" or "o to" ? "CAR" : "MOTORCYCLE";
        decimal radius = 5; bool available = false; string? lot = null;
        DateTimeOffset? start = null, end = null; long? min = null, max = null;
        var seen = new HashSet<string>();
        var options = match.Groups[4].Value;
        if (Regex.IsMatch(options, $@"^{Now}$")) options = "";
        foreach (var option in options.Split(' ', StringSplitOptions.RemoveEmptyEntries))
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

    private static bool Location(string latitude, string longitude, out decimal lat, out decimal lon)
    {
        lon = default;
        return Number(latitude, out lat) && lat is >= -90 and <= 90 &&
            Number(longitude, out lon) && lon is >= -180 and <= 180;
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
