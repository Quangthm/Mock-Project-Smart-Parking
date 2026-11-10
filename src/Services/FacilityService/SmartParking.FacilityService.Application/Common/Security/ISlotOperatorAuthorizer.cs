namespace SmartParking.FacilityService.Application.Common.Security;

public interface ISlotOperatorAuthorizer
{
    Task<OwnerScope> ScopeAsync(Guid site, CancellationToken cancellationToken);
    Task<bool> IsAuthorizedAsync(OwnerScope actor, Guid site, CancellationToken cancellationToken);
}
