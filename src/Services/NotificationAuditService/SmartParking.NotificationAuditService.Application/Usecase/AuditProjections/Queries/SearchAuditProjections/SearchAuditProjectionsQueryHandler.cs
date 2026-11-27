using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Models;
using SmartParking.NotificationAuditService.Application.DTOs.Audit;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.SearchAuditProjections;

public sealed class SearchAuditProjectionsQueryHandler
    : IRequestHandler<SearchAuditProjectionsQuery, AuditProjectionPageDto>
{
    private const string RetentionPolicyText = "7 years (immutable append-only projection)";
    private const string ReadModelNoticeText =
        "Audit projections are populated asynchronously from domain events; missing or delayed events may reflect ingestion lag rather than absence of activity.";

    private readonly ICentralAuditProjectionRepository _repository;
    private readonly ILogger<SearchAuditProjectionsQueryHandler> _logger;

    public SearchAuditProjectionsQueryHandler(
        ICentralAuditProjectionRepository repository,
        ILogger<SearchAuditProjectionsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AuditProjectionPageDto> Handle(
        SearchAuditProjectionsQuery request,
        CancellationToken cancellationToken)
    {
        var criteria = new AuditProjectionSearchCriteria(
            ActorId: request.ActorId,
            ActorRole: request.ActorRole?.Trim(),
            Action: request.Action?.Trim(),
            OriginatingService: request.OriginatingService?.Trim(),
            ResourceType: request.ResourceType?.Trim(),
            ResourceId: request.ResourceId,
            LotId: request.LotId,
            Search: request.Search?.Trim(),
            From: request.From,
            To: request.To,
            Page: request.Page,
            PageSize: request.PageSize);

        var (items, totalCount) = await _repository.SearchAsync(criteria, cancellationToken);

        var mappedItems = items
            .Select(AuditProjectionMapper.ToRedactedDto)
            .ToList();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        _logger.LogInformation(
            "Admin audit search executed: Page={Page}, PageSize={PageSize}, Returned={ReturnedCount}, TotalCount={TotalCount}",
            request.Page,
            request.PageSize,
            mappedItems.Count,
            totalCount);

        return new AuditProjectionPageDto(
            Items: mappedItems,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize,
            TotalPages: totalPages,
            ReadModel: new AuditReadModelMetadataDto(
                IsAsynchronousProjection: true,
                RetentionPolicy: RetentionPolicyText,
                Notice: ReadModelNoticeText,
                QueriedAt: DateTimeOffset.UtcNow));
    }
}
