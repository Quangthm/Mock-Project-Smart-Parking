using System.Globalization;
using System.Text.Json;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application;

public sealed record DiscoverySchedule(string TimeZone, List<TimeBlockRule> Blocks, int? DiscoveryGraceMinutes)
{
    public static DiscoverySchedule Read(string policyJson)
    {
        using var doc = JsonDocument.Parse(policyJson);
        var root = doc.RootElement;
        if (root.GetProperty("timeZoneId").GetString() != "Asia/Ho_Chi_Minh" ||
            root.GetProperty("roundingMode").GetString() != "HALF_UP_THOUSAND") throw Invalid();
        var rules = root.GetProperty("timeBlocks");
        if (rules.GetArrayLength() is < 1 or > 1440) throw Invalid();
        var blocks = new List<TimeBlockRule>();
        foreach (var item in rules.EnumerateArray())
        {
            var start = TimeOnly.ParseExact(item.GetProperty("startTime").GetString()!, "HH:mm", CultureInfo.InvariantCulture);
            var end = TimeOnly.ParseExact(item.GetProperty("endTime").GetString()!, "HH:mm", CultureInfo.InvariantCulture);
            blocks.Add(new(item.GetProperty("blockCode").GetString()!, start, end, item.GetProperty("ratePerHourVnd").GetInt64()));
        }
        ParkingPricingCalculator.ValidateTimeBlocks(blocks);
        int? grace = null;
        if (root.TryGetProperty("discoveryEstimate", out var terms))
        {
            var minutes = terms.GetProperty("graceMinutes").GetInt32();
            var minimum = terms.GetProperty("minimumVnd").GetInt64();
            if (minutes < 0 || minimum < 0) throw Invalid();
            // A nonzero minimum requires a different estimate policy.
            if (minimum == 0) grace = minutes;
        }
        return new("Asia/Ho_Chi_Minh", blocks, grace);
    }

    public long RateAt(DateTimeOffset at)
    {
        var time = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(at, TimeZone).DateTime);
        return Blocks.Single(b => b.StartTime == b.EndTime || (b.StartTime < b.EndTime
            ? time >= b.StartTime && time < b.EndTime : time >= b.StartTime || time < b.EndTime)).RatePerHourVnd;
    }
    private static PricingDomainException Invalid() => new("INVALID_PRICING_POLICY", "Invalid discovery schedule.");
}
