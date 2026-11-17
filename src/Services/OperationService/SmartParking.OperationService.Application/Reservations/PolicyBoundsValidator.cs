namespace SmartParking.OperationService.Application.Reservations;

internal static class PolicyBoundsValidator
{
    private static readonly HashSet<string> OverridableKeys = new(StringComparer.Ordinal)
    {
        "RESERVATION_MIN_DURATION", "RESERVATION_MAX_DURATION", "MAX_ADVANCE_BOOKING", "INTER_BOOKING_BUFFER",
        "PAYMENT_HOLD_DURATION", "RESERVATION_PROTECTION_WINDOW", "ALLOCATION_LEAD_TIME",
        "FACILITY_FAILURE_REFUND_LEAD_TIME", "OCCUPANCY_VALIDITY_TIMESPAN", "ARRIVAL_LATE_TOLERANCE"
    };

    private static readonly HashSet<string> PositiveMinimumKeys = new(StringComparer.Ordinal)
    {
        "RESERVATION_MIN_DURATION", "PAYMENT_HOLD_DURATION"
    };

    private static readonly HashSet<string> NonNegativeMinimumKeys = new(StringComparer.Ordinal)
    {
        "INTER_BOOKING_BUFFER", "ALLOCATION_LEAD_TIME", "FACILITY_FAILURE_REFUND_LEAD_TIME",
        "RESERVATION_MAX_DURATION", "RESERVATION_PROTECTION_WINDOW", "OCCUPANCY_VALIDITY_TIMESPAN",
        "MAX_ADVANCE_BOOKING"
    };

    public static void Validate(IReadOnlyDictionary<string, PolicyBoundDto>? bounds)
    {
        if (bounds is null) return;
        var errors = new List<(string Property, string Message)>();
        foreach (var (key, bound) in bounds)
        {
            var property = $"overrideBounds.{key}";
            if (!OverridableKeys.Contains(key))
            {
                errors.Add((property, $"Override bounds are not permitted for policy '{key}'."));
                continue;
            }

            if (bound.Minimum > bound.Maximum)
            {
                errors.Add((property, "Override minimum must not exceed maximum."));
                continue;
            }

            if (PositiveMinimumKeys.Contains(key) && bound.Minimum < 1)
                errors.Add((property, "Override minimum must be at least 1."));
            else if (NonNegativeMinimumKeys.Contains(key) && bound.Minimum < 0)
                errors.Add((property, "Override minimum must not be negative."));

            switch (key)
            {
                case "ARRIVAL_LATE_TOLERANCE":
                    if (bound.Minimum < 15 || bound.Maximum > 30)
                        errors.Add((property, "Arrival late tolerance bounds must stay within 15 and 30 minutes."));
                    break;
                case "RESERVATION_MAX_DURATION":
                case "RESERVATION_PROTECTION_WINDOW":
                case "OCCUPANCY_VALIDITY_TIMESPAN":
                {
                    var requiredMinimum = key == "RESERVATION_MAX_DURATION"
                        ? Math.Max(1, CeilDiv(bound.Minimum, 60))
                        : Math.Max(0, CeilDiv(bound.Minimum, 60));
                    if (FloorDiv(bound.Maximum, 60) < requiredMinimum)
                        errors.Add((property, "Override bounds cannot represent any whole-hour value."));
                    break;
                }
                case "MAX_ADVANCE_BOOKING":
                    if (FloorDiv(bound.Maximum, 1440) < Math.Max(0, CeilDiv(bound.Minimum, 1440)))
                        errors.Add((property, "Override bounds cannot represent any whole-day value."));
                    break;
            }
        }

        if (errors.Count != 0)
            throw new InvalidPolicyBoundsException("One or more policy override bounds are invalid.", errors);
    }

    private static int CeilDiv(int value, int divisor) => value <= 0 ? 0 : 1 + (value - 1) / divisor;

    private static int FloorDiv(int value, int divisor) => value < 0 ? 0 : value / divisor;
}