namespace SmartParking.NotificationAuditService.Application.Contracts.Notifications;

public record PaymentVerifiedNotificationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid PaymentOrderId { get; init; }
    public Guid DriverId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public string PaymentMethod { get; init; } = "PAYOS";
    public string TransactionId { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
