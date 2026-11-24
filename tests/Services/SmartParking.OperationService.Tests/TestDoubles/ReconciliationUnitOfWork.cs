using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests.TestDoubles;

// Unit of work over a real ReservationDbContext. Guards are asserted separately, so this double
// deliberately skips UnitOfWork's advisory-lock preconditions while still persisting real rows.
internal sealed class ReconciliationUnitOfWork : IUnitOfWork
{
    private readonly ReservationDbContext _db;
    private readonly SlotAllocationRepository _allocations;

    public ReconciliationUnitOfWork(ReservationDbContext db)
    {
        _db = db;
        _allocations = new SlotAllocationRepository(db);
    }

    public ReconciliationTransaction Transaction { get; } = new();
    public int SaveChangesCalls { get; private set; }

    public IReservationRepository Reservations => throw new NotSupportedException();
    public IParkingSessionRepository ParkingSessions => throw new NotSupportedException();
    public ISlotAllocationRepository SlotAllocations => _allocations;

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IUnitOfWorkTransaction>(Transaction);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return _db.SaveChangesAsync(cancellationToken);
    }
}