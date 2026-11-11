namespace SmartParking.NotificationAuditService.Application.DTOs.Notifications;

public record NotificationDeliveryResult
{
    public bool Success { get; init; }
    public Guid? NotificationId { get; init; }
    public int Attempts { get; init; }
    public string? Status { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsDuplicate { get; init; }

    public static NotificationDeliveryResult Duplicate(Guid notificationId) =>
        new() { Success = true, NotificationId = notificationId, IsDuplicate = true, Status = "DUPLICATE_ACKNOWLEDGED" };

    public static NotificationDeliveryResult Delivered(Guid notificationId, int attempts) =>
        new() { Success = true, NotificationId = notificationId, Attempts = attempts, Status = "DELIVERED" };

    public static NotificationDeliveryResult Failed(Guid notificationId, int attempts, string error) =>
        new() { Success = false, NotificationId = notificationId, Attempts = attempts, Status = "FAILED", ErrorMessage = error };
}
