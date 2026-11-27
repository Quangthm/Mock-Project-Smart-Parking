using System.Collections.ObjectModel;

namespace SmartParking.OperationService.Domain.Reservations;

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
