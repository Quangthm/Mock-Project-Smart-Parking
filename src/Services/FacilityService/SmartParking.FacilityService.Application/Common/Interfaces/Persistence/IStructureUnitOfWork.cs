using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

/// <summary>
/// Domain-Specific Unit of Work and Distributed Edit Coordinator for the ParkingStructure Aggregate Root.
/// Coordinates multi-step two-phase holds with OperationService, distributed site locks,
/// crash recovery, and local database transactions.
/// Kept strictly segregated from generic DbContext IUnitOfWork to satisfy the Interface Segregation Principle (ISP).
/// </summary>
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
