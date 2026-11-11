namespace SmartParking.OperationService.Domain.Reservations;

/// <summary>A half-open interval. Resource identity and occupied visits are checked by the caller.</summary>
public sealed record ReservationInterval
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public ReservationInterval(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new ArgumentException("Reservation end must follow its start.");
        Start = start.ToUniversalTime();
        End = end.ToUniversalTime();
    }

    public bool ConflictsWith(ReservationInterval other, TimeSpan requiredGap)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (requiredGap < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(requiredGap));

        // Compare the gap without adding a duration to a potentially maximal timestamp.
        if (End <= other.Start)
            return other.Start - End < requiredGap;
        if (other.End <= Start)
            return Start - other.End < requiredGap;
        return true;
    }

    public bool MeetsBookingWindow(DateTimeOffset now, TimeSpan minimumDuration,
        TimeSpan maximumDuration, TimeSpan advanceHorizon)
    {
        if (minimumDuration <= TimeSpan.Zero || maximumDuration < minimumDuration || advanceHorizon < TimeSpan.Zero)
            throw new ArgumentException("Effective booking limits are invalid.");
        var duration = End - Start;
        return Start > now && Start - now <= advanceHorizon &&
            duration >= minimumDuration && duration <= maximumDuration;
    }
}
