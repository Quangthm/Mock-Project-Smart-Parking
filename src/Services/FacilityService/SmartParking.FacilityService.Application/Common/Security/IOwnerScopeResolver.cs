using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Security;

public interface IOwnerScopeResolver
{
    Task<OwnerScope> ResolveAsync(Guid? tenantId, CancellationToken ct, string permission = "PARKING_MANAGE");
}
