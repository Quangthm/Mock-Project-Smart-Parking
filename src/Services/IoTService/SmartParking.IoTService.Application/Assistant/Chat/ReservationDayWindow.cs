namespace SmartParking.IoTService.Application.Assistant.Chat;

// Pure preparation for owner read queries. This does not query or authorize reservations.
public sealed record ReservationDayWindow(DateOnly LocalDate, string TimeZone, DateTimeOffset From, DateTimeOffset To)
{
    public static ReservationDayWindow Tomorrow(TimeProvider clock, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        return ForDate(today.AddDays(1), zone);
    }

    public static ReservationDayWindow ForDate(DateOnly date, TimeZoneInfo zone)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // Do not guess an offset at a midnight DST transition; the caller must clarify.
        if (zone.IsInvalidTime(start) || zone.IsAmbiguousTime(start) || zone.IsInvalidTime(end) || zone.IsAmbiguousTime(end))
            throw new ChatFailure(400, "AMBIGUOUS_TIME_RANGE");
        return new(date, zone.Id, new(TimeZoneInfo.ConvertTimeToUtc(start, zone)), new(TimeZoneInfo.ConvertTimeToUtc(end, zone)));
    }

    public bool Overlaps(DateTimeOffset startAt, DateTimeOffset endAt) =>
        endAt > startAt && startAt < To && endAt > From;
}
