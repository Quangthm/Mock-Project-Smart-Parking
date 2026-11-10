using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Security;

public sealed class OwnerScopeResolver(IOwnerScopeReader reader) : IOwnerScopeResolver
{
    public async Task<OwnerScope> ResolveAsync(Guid? tenantId, CancellationToken ct, string permission = "PARKING_MANAGE")
    {
        var scope = await reader.ScopeAsync(ct, permission);
        var selected = tenantId ?? (scope.TenantIds.Length == 1 ? scope.TenantIds[0] : Guid.Empty);
        if (!scope.TenantIds.Contains(selected))
            throw new StructureException("FORBIDDEN", "Select an authorized tenant.");
        return new(scope.UserId, selected);
    }
}
