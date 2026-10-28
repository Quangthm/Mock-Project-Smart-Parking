using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Domain.Entities;

public class CentralAuditProjection
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OriginatingService { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public Guid ActorId { get; private set; }
    public string ActorRole { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public string Details { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ProjectedAt { get; private set; } = DateTimeOffset.UtcNow;

    // EF Core parameterless constructor
    protected CentralAuditProjection() { }

    public static CentralAuditProjection Create(
        string originatingService,
        Guid eventId,
        Guid actorId,
        string actorRole,
        string action,
        string resourceType,
        Guid resourceId,
        string details,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(originatingService))
            throw new NotificationDomainException("OriginatingService cannot be empty.");
        if (eventId == Guid.Empty)
            throw new NotificationDomainException("EventId cannot be empty.");
        if (actorId == Guid.Empty)
            throw new NotificationDomainException("ActorId cannot be empty.");
        if (string.IsNullOrWhiteSpace(actorRole))
            throw new NotificationDomainException("ActorRole cannot be empty.");
        if (string.IsNullOrWhiteSpace(action))
            throw new NotificationDomainException("Action cannot be empty.");
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new NotificationDomainException("ResourceType cannot be empty.");
        if (resourceId == Guid.Empty)
            throw new NotificationDomainException("ResourceId cannot be empty.");

        return new CentralAuditProjection
        {
            OriginatingService = originatingService.Trim(),
            EventId = eventId,
            ActorId = actorId,
            ActorRole = actorRole.Trim(),
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = resourceId,
            Details = string.IsNullOrWhiteSpace(details) ? "{}" : details.Trim(),
            OccurredAt = occurredAt,
            ProjectedAt = DateTimeOffset.UtcNow
        };
    }
}
