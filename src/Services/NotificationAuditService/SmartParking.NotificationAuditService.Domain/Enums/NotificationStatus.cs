namespace SmartParking.NotificationAuditService.Domain.Enums;

public enum NotificationStatus
{
    PENDING,
    PROCESSING,
    DELIVERED,
    FAILED,
    DEAD_LETTER
}
