using MediatR;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationPolicyRepository
{
    Task<IReadOnlyList<ReservationPolicyRevision>> ReadEffectiveAsync(Guid tenantId, Guid siteId, DateTimeOffset at, CancellationToken ct);
}

// Internal composition request: callers must first obtain trusted tenant/site facts from Facility.
// No policy-management or arbitrary tenant-selected HTTP endpoint is exposed by this task.
public sealed record ResolveReservationPoliciesQuery(Guid TenantId, Guid SiteId, DateTimeOffset At)
    : IRequest<EffectiveReservationPolicy>;

public sealed class ResolveReservationPoliciesHandler(IReservationPolicyRepository policies)
    : IRequestHandler<ResolveReservationPoliciesQuery, EffectiveReservationPolicy>
{
    public async Task<EffectiveReservationPolicy> Handle(ResolveReservationPoliciesQuery query, CancellationToken ct)
    {
        if (query.TenantId == Guid.Empty || query.SiteId == Guid.Empty)
            throw new ArgumentException("Trusted tenant/site scope is required.");
        var revisions = await policies.ReadEffectiveAsync(query.TenantId, query.SiteId, query.At, ct);
        var platform = revisions.Where(x => x.TenantId is null && x.SiteId is null).ToArray();
        var site = revisions.Where(x => x.TenantId == query.TenantId && x.SiteId == query.SiteId).ToArray();
        if (platform.Length != 1 || site.Length > 1)
            throw new ReservationPolicyUnavailableException("Effective policy history is absent or ambiguous.");
        return ReservationPolicyResolver.Resolve(platform[0], site.SingleOrDefault());
    }
}
