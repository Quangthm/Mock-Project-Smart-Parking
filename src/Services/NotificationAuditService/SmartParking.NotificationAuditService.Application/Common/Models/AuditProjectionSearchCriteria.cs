namespace SmartParking.NotificationAuditService.Application.Common.Models;

public sealed record AuditProjectionSearchCriteria(
    Guid? ActorId,
    string? ActorRole,
    string? Action,
    string? OriginatingService,
    string? ResourceType,
    Guid? ResourceId,
    Guid? LotId,
    string? Search,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);
