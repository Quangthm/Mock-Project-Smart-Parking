using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Audit;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections;

public record RecordAuditProjectionCommand : IRequest<RecordAuditProjectionResult>
{
    public Guid EventId { get; init; }
    public string OriginatingService { get; init; } = string.Empty;
    public Guid ActorId { get; init; }
    public string ActorRole { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public Guid ResourceId { get; init; }
    public string Details { get; init; } = "{}";
    public DateTimeOffset OccurredAt { get; init; }
}
