namespace SmartParking.FacilityService.Application.Common.Security;

public interface IOwnerAuthorizer
{
    Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken cancellationToken);
}
