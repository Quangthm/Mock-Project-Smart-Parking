namespace SmartParking.NotificationAuditService.Application.Contracts.Notifications;

public record ReservationConfirmedNotificationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid ReservationId { get; init; }
    public Guid DriverId { get; init; }
    public Guid SiteId { get; init; }
    public string SiteName { get; init; } = string.Empty;
    public string SlotCode { get; init; } = string.Empty;
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
