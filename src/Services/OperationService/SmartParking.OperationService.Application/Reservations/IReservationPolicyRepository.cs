using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record EffectivePolicyLookup(ReservationPolicyRevision? Platform, ReservationPolicyRevision? Site,
    bool TenantSiteMismatch);

public sealed record PolicyActivationOutcome(OperationalPolicyRow Target, OperationalPolicyRow? Superseded);

public interface IReservationPolicyRepository
{
    Task<IReadOnlyList<ReservationPolicyRevision>> ReadEffectiveAsync(Guid tenantId, Guid siteId, DateTimeOffset at,
        CancellationToken ct);

    Task<PlatformPolicyCatalogueDto> GetCatalogueAsync(CancellationToken ct) =>
        throw new NotSupportedException();

    Task<ReservationPolicyRevision?> GetActivePlatformRevisionAsync(DateTimeOffset at, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<EffectivePolicyLookup> GetEffectivePolicyAsync(Guid? tenantId, Guid? siteId, DateTimeOffset at,
        CancellationToken ct) =>
        throw new NotSupportedException();

    Task<PagedResult<PolicyRevisionListItemDto>> GetRevisionsAsync(string status, string scope, Guid? tenantId,
        Guid? siteId, int page, int pageSize, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<PolicyRevisionListItemDto?> GetRevisionByIdAsync(Guid id, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<int> GetNextPlatformVersionAsync(CancellationToken ct) =>
        throw new NotSupportedException();

    Task CreateDraftAsync(OperationalPolicyRow draft, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<DateTimeOffset> UpdateDraftOptimisticAsync(Guid id, OperationalPolicyRow updated,
        DateTimeOffset expectedUpdatedAt, CancellationToken ct) =>
        throw new NotSupportedException();

    Task<PolicyActivationOutcome> ActivateRevisionAsync(Guid id, DateTimeOffset effectiveFrom, CancellationToken ct) =>
        throw new NotSupportedException();
}