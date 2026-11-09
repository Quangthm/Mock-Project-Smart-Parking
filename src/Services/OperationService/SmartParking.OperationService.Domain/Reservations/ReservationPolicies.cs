using System.Collections.ObjectModel;

namespace SmartParking.OperationService.Domain.Reservations;

public sealed record PolicyBound(int Minimum, int Maximum);
public sealed record ReservationPolicyRevision(Guid Id, int Version, Guid? TenantId, Guid? SiteId,
    IReadOnlyDictionary<string, int> Values, IReadOnlyDictionary<string, PolicyBound> OverrideBounds,
    bool? ArrivalLateToleranceEnabled, string? CancellationTimezone);

public sealed record PolicyRevisionReference(Guid Id, int Version);

public sealed class EffectiveReservationPolicy
{
    public PolicyRevisionReference Platform { get; }
    public PolicyRevisionReference Site { get; }
    public IReadOnlyDictionary<string, int> Values { get; }
    public bool ArrivalLateToleranceEnabled { get; }
    public string CancellationTimezone { get; }

    internal EffectiveReservationPolicy(ReservationPolicyRevision platform, ReservationPolicyRevision site, Dictionary<string, int> values)
    {
        Platform = new(platform.Id, platform.Version);
        Site = new(site.Id, site.Version);
        Values = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(values));
        ArrivalLateToleranceEnabled = site.ArrivalLateToleranceEnabled!.Value;
        CancellationTimezone = platform.CancellationTimezone!;
    }

    public ReservationDeadlines DeadlinesFor(ReservationInterval interval, DateTimeOffset holdCreatedAt) => new(interval,
        holdCreatedAt, TimeSpan.FromMinutes(Values["ALLOCATION_LEAD_TIME"]),
        TimeSpan.FromMinutes(Values["FACILITY_FAILURE_REFUND_LEAD_TIME"]),
        TimeSpan.FromMinutes(Values["PAYMENT_HOLD_DURATION"]));

    public bool AllowsWindow(ReservationInterval interval, DateTimeOffset now) => interval.MeetsBookingWindow(now,
        TimeSpan.FromMinutes(Values["RESERVATION_MIN_DURATION"]), TimeSpan.FromMinutes(Values["RESERVATION_MAX_DURATION"]),
        TimeSpan.FromMinutes(Values["MAX_ADVANCE_BOOKING"]));
}

public sealed class ReservationPolicyUnavailableException(string message) : Exception(message);

public static class ReservationPolicyResolver
{
    // Values are minutes except COUNT/LIMIT. These are required keys, never runtime business defaults.
    public static readonly IReadOnlyList<string> RequiredKeys = Array.AsReadOnly(new[]
    {
        "RESERVATION_MIN_DURATION", "RESERVATION_MAX_DURATION", "MAX_ADVANCE_BOOKING", "INTER_BOOKING_BUFFER",
        "PAYMENT_HOLD_DURATION", "RESERVATION_PROTECTION_WINDOW", "ALLOCATION_LEAD_TIME",
        "FACILITY_FAILURE_REFUND_LEAD_TIME", "OCCUPANCY_VALIDITY_TIMESPAN", "ARRIVAL_LATE_TOLERANCE",
        "RAPID_CANCELLATION_COUNT", "RAPID_CANCELLATION_WINDOW", "RAPID_CANCELLATION_RESTRICTION_DURATION", "DAILY_CANCELLATION_LIMIT"
    });

    public static EffectiveReservationPolicy Resolve(ReservationPolicyRevision? platform, ReservationPolicyRevision? site)
    {
        if (platform is null || platform.TenantId is not null || platform.SiteId is not null ||
            site is null || site.TenantId is null || site.SiteId is null || site.ArrivalLateToleranceEnabled is null)
            throw new ReservationPolicyUnavailableException("Platform policy and explicit site late-tolerance choice are required.");
        if (platform.Version < 1 || site.Version < 1 || platform.Id == Guid.Empty || site.Id == Guid.Empty)
            throw new ReservationPolicyUnavailableException("Policy revision identity is invalid.");
        if (platform.CancellationTimezone != "Asia/Ho_Chi_Minh" || site.CancellationTimezone is not null)
            throw new ReservationPolicyUnavailableException("Cancellation timezone must retain the approved platform policy.");
        var values = new Dictionary<string, int>();
        foreach (var key in RequiredKeys)
        {
            if (!platform.Values.TryGetValue(key, out var value))
                throw new ReservationPolicyUnavailableException($"Required policy {key} is missing.");
            if (site.Values.TryGetValue(key, out var overridden) && overridden != value)
            {
                if (key.StartsWith("RAPID_CANCELLATION_", StringComparison.Ordinal) || key == "DAILY_CANCELLATION_LIMIT" ||
                    !platform.OverrideBounds.TryGetValue(key, out var bound) || bound.Minimum > bound.Maximum ||
                    overridden < bound.Minimum || overridden > bound.Maximum)
                    throw new ReservationPolicyUnavailableException($"Policy {key} override has no applicable approved bounds.");
                value = overridden;
            }
            if (value < 0) throw new ReservationPolicyUnavailableException($"Policy {key} cannot be negative.");
            values.Add(key, value);
        }
        if (values["RESERVATION_MIN_DURATION"] == 0 || values["RESERVATION_MAX_DURATION"] < values["RESERVATION_MIN_DURATION"] ||
            values["PAYMENT_HOLD_DURATION"] == 0 || values["RAPID_CANCELLATION_COUNT"] == 0 ||
            values["DAILY_CANCELLATION_LIMIT"] == 0 || values["ARRIVAL_LATE_TOLERANCE"] is < 15 or > 30)
            throw new ReservationPolicyUnavailableException("Effective reservation policy is inconsistent.");
        return new EffectiveReservationPolicy(platform, site, values);
    }
}
