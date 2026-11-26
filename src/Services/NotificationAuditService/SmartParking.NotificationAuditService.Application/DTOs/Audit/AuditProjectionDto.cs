namespace SmartParking.NotificationAuditService.Application.DTOs.Audit;

public sealed record AuditProjectionDto(
    Guid Id,
    string OriginatingService,
    Guid EventId,
    Guid ActorId,
    string ActorRole,
    string Action,
    string ResourceType,
    Guid ResourceId,
    string Details,
    DateTimeOffset OccurredAt,
    DateTimeOffset ProjectedAt,
    double IngestionLagSeconds);
