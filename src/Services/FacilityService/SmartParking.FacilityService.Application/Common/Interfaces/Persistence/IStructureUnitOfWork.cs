using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public interface IStructureUnitOfWork
{
    Task<IStructureEditSession> BeginEditAsync(
        OwnerScope owner, Guid siteId, CancellationToken ct,
        Guid? operationId = null, Guid? markedSlot = null, string? reason = null);

    Task RecoverAsync(OwnerScope owner, Guid siteId, CancellationToken ct);
}

public interface IStructureEditSession : IAsyncDisposable
{
    ParkingStructure Structure { get; }
    Task CommitAsync(CancellationToken ct);
}
