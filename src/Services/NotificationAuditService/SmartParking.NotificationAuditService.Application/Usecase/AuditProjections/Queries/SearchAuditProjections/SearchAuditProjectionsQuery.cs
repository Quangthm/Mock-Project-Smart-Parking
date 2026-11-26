using MediatR;
using SmartParking.NotificationAuditService.Application.DTOs.Audit;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.SearchAuditProjections;

public sealed record SearchAuditProjectionsQuery(
    Guid? ActorId = null,
    string? ActorRole = null,
    string? Action = null,
    string? OriginatingService = null,
    string? ResourceType = null,
    Guid? ResourceId = null,
    Guid? LotId = null,
    string? Search = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<AuditProjectionPageDto>;
