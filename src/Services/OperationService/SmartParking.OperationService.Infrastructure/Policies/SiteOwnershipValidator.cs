using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Policies.Ports;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Infrastructure.Policies;

public sealed class SiteOwnershipValidator(
    ReservationDbContext db,
    IOwnerIdentityClient identityClient) : ISiteOwnershipValidator
{
    public async Task<Guid> ValidateOwnershipAsync(Guid? actorId, Guid siteId, CancellationToken ct)
    {
        // 1. Resolve owner's authorized tenant scopes via UserService first (AuthN / Scope check)
        var scope = await identityClient.GetOwnerScopeAsync("PARKING_MANAGE", ct);

        // 2. Verify parking lot existence in structure_site_state, falling back to site_capacity_pools or operational_policies
        Guid? siteTenantId = await db.StructureSiteStates
            .AsNoTracking()
            .Where(s => s.SiteId == siteId)
            .Select(s => (Guid?)s.TenantId)
            .FirstOrDefaultAsync(ct);

        if (!siteTenantId.HasValue)
        {
            siteTenantId = await db.SiteCapacityPools
                .AsNoTracking()
                .Where(s => s.SiteId == siteId)
                .Select(s => (Guid?)s.TenantId)
                .FirstOrDefaultAsync(ct);
        }

        if (!siteTenantId.HasValue)
        {
            siteTenantId = await db.Set<OperationalPolicyRow>()
                .AsNoTracking()
                .Where(s => s.SiteId == siteId && s.TenantId != null)
                .Select(s => s.TenantId)
                .FirstOrDefaultAsync(ct);
        }

        if (!siteTenantId.HasValue || siteTenantId.Value == Guid.Empty)
        {
            throw new LotPolicyNotFoundException("SITE_NOT_FOUND", "Parking lot not found.");
        }

        // 3. Verify cross-lot permission: site's TenantId must belong to owner's authorized TenantIds
        if (!scope.TenantIds.Contains(siteTenantId.Value))
        {
            throw new CrossLotAccessException("You do not own this parking lot.");
        }

        return siteTenantId.Value;
    }
}
