namespace SmartParking.OperationService.Application.Reservations;

internal static class PolicyTimestamps
{
    public static DateTimeOffset ToMicroseconds(this DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % 10), value.Offset);
}