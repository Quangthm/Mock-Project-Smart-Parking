using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public interface IParkingStructureRepository
{
    Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken cancellationToken);
    Task<ParkingStructure> ReadAsync(OwnerScope owner, Guid siteId, CommitmentSnapshot? snapshot, CancellationToken cancellationToken);
    Task<SiteProfile> CreateAsync(OwnerScope owner, SiteProfile site, CancellationToken cancellationToken);
    Task<IParkingStructureEdit> BeginEditAsync(
        OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, Guid? operationId, CancellationToken cancellationToken);
}

// Application owns the use case; Persistence owns this short local transaction.
// Disposing an edit without committing rolls it back.
public interface IParkingStructureEdit : IAsyncDisposable
{
    ParkingStructure Structure { get; }
    Task CommitAsync(
        Guid synchronizationToken, StructureOutcome outcome,
        Guid? markedSlot, string? reason, CancellationToken cancellationToken);
}
