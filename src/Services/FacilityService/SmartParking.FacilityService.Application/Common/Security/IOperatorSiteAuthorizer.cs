namespace SmartParking.FacilityService.Application.Common.Security;

public interface IOperatorSiteAuthorizer
{
    Task<OwnerScope> ScopeAsync(Guid siteId, CancellationToken ct);
}
