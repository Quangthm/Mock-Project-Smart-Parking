namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface IUnitOfWork
{
    IReservationRepository Reservations { get; }
    IParkingSessionRepository ParkingSessions { get; }
    ISlotAllocationRepository SlotAllocations { get; }
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
