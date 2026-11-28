using SmartParking.NotificationAuditService.Application.Common.Security;
using SmartParking.NotificationAuditService.Application.DTOs.Audit;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries;

internal static class AuditProjectionMapper
{
    public static AuditProjectionDto ToRedactedDto(CentralAuditProjection projection)
    {
        var lagSeconds = Math.Max(0d, Math.Round((projection.ProjectedAt - projection.OccurredAt).TotalSeconds, 3));

        return new AuditProjectionDto(
            Id: projection.Id,
            OriginatingService: projection.OriginatingService,
            EventId: projection.EventId,
            ActorId: projection.ActorId,
            ActorRole: projection.ActorRole,
            Action: projection.Action,
            ResourceType: projection.ResourceType,
            ResourceId: projection.ResourceId,
            Details: AuditDetailsRedactor.Redact(projection.Details),
            OccurredAt: projection.OccurredAt,
            ProjectedAt: projection.ProjectedAt,
            IngestionLagSeconds: lagSeconds);
    }
}
