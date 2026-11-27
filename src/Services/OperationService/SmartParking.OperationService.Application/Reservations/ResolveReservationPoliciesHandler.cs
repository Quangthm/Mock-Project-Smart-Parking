using MediatR;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ResolveReservationPoliciesHandler(IReservationPolicyRepository policies)
    : IRequestHandler<ResolveReservationPoliciesQuery, EffectiveReservationPolicy>
{
    public async Task<EffectiveReservationPolicy> Handle(ResolveReservationPoliciesQuery query, CancellationToken ct)
    {
        if (query.TenantId == Guid.Empty || query.SiteId == Guid.Empty)
            throw new ArgumentException("Trusted tenant/site scope is required.");
        var revisions = await policies.ReadEffectiveAsync(query.TenantId, query.SiteId, query.At, ct);
        return ReservationPolicyResolver.ResolveForSite(revisions, query.TenantId, query.SiteId);
    }
}
