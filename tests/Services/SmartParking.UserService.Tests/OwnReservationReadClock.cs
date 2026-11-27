namespace SmartParking.UserService.Tests;

internal sealed class OwnReservationReadClock : TimeProvider
{
    public DateTimeOffset Now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
