using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

// The Application edit handler owns orchestration. The repository edit owns its local
// transaction; the session keeps its lifetime until the outcome is durably recorded.
public interface IStructureEditSession : IAsyncDisposable
{
    ParkingStructure Structure { get; }
    Task CommitAsync(CancellationToken ct);
}
