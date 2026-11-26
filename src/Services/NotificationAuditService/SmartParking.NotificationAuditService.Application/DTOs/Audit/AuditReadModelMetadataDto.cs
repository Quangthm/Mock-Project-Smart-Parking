namespace SmartParking.NotificationAuditService.Application.DTOs.Audit;

public sealed record AuditReadModelMetadataDto(
    bool IsAsynchronousProjection,
    string RetentionPolicy,
    string Notice,
    DateTimeOffset QueriedAt);
