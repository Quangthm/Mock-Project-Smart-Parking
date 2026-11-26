namespace SmartParking.OperationService.Application.Policies.Ports;

public interface ISiteOwnershipValidator
{
    Task<Guid> ValidateOwnershipAsync(Guid? actorId, Guid siteId, CancellationToken ct);
}
