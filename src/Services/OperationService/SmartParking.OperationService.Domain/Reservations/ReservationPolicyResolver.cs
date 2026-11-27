namespace SmartParking.OperationService.Domain.Reservations;

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

    public static EffectiveReservationPolicy ResolveForSite(IReadOnlyList<ReservationPolicyRevision> revisions,
        Guid tenantId, Guid siteId)
    {
        var platform = revisions.Where(x => x.TenantId is null && x.SiteId is null).ToArray();
        var site = revisions.Where(x => x.TenantId == tenantId && x.SiteId == siteId).ToArray();
        if (platform.Length != 1 || site.Length > 1)
            throw new ReservationPolicyUnavailableException("Effective policy history is absent or ambiguous.");
        return Resolve(platform[0], site.SingleOrDefault());
    }

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
