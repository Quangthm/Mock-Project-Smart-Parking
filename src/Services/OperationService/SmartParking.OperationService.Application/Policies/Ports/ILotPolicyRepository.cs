using SmartParking.OperationService.Domain.Policies;

namespace SmartParking.OperationService.Application.Policies.Ports;

public sealed record PolicyCatalogueData(
    Guid SiteId,
    int ActiveRevision,
    DateTimeOffset ActiveSince,
    bool HasDraft,
    IReadOnlyList<PolicyCatalogueItem> Policies
);

public sealed record PolicyPublishResult(
    Guid SiteId,
    int NewRevision,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset PublishedAt,
    string? PublishNote,
    string Message,
    bool IsReplayed = false
);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public interface ILotPolicyRepository
{
    Task<PolicyCatalogueData> GetCatalogueAsync(Guid siteId, CancellationToken ct);
    Task<PolicyDraftEntity?> GetDraftAsync(Guid siteId, CancellationToken ct);
    Task<PolicyDraftEntity> SaveDraftAsync(Guid siteId, Guid tenantId, Guid actorId, PolicyDraftConfig config, DateTimeOffset? scheduledAt, CancellationToken ct);
    Task<PolicyPublishResult> PublishDraftAsync(Guid siteId, Guid tenantId, Guid actorId, string mode, DateTimeOffset? scheduledAt, string? note, string? idempotencyKey, CancellationToken ct);
    Task<PagedResult<PolicyRevisionSummary>> GetRevisionsAsync(Guid siteId, int page, int pageSize, CancellationToken ct);
    Task<PolicySnapshot?> GetRevisionSnapshotAsync(Guid siteId, int version, CancellationToken ct);
}
