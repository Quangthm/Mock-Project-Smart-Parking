using SmartParking.ParkingService.Domain;
namespace SmartParking.ParkingService.Application;

public interface IOperatorSiteAuthorizer
{
    Task<OwnerScope> ScopeAsync(Guid siteId, CancellationToken ct);
}

public sealed class OperatorLayoutService(
    IOperatorSiteAuthorizer operators, IParkingStructureRepository repository,
    IStructureCommitments commitments)
{
    public async Task<ParkingStructure> ReadAsync(Guid siteId, CancellationToken ct)
    {
        var actor = await operators.ScopeAsync(siteId, ct);
        // Read-only dashboard projection: never acquires a structure-edit hold.
        var snapshot = await commitments.ReadSnapshotAsync(actor.TenantId, siteId, ct);
        return await repository.ReadAsync(actor, siteId, snapshot, ct);
    }
}
