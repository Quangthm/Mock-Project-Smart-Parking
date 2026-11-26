namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationDeadlineResult
{
    private ReservationDeadlineResult(bool applied, TimeSpan? remaining) => (Applied, Remaining) = (applied, remaining);
    public bool Applied { get; }
    public TimeSpan? Remaining { get; }
    public static ReservationDeadlineResult Completed { get; } = new(true, null);
    public static ReservationDeadlineResult Obsolete { get; } = new(false, null);
    public static ReservationDeadlineResult Early(TimeSpan remaining) => remaining > TimeSpan.Zero
        ? new(false, remaining) : throw new ArgumentOutOfRangeException(nameof(remaining));
}
