using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.ReservationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.ReservationService.Persistence.Repositories;

public sealed class UnitOfWork(
    ReservationDbContext db,
    IReservationRepository reservations,
    IParkingSessionRepository parkingSessions,
    ISlotAllocationRepository slotAllocations) : IUnitOfWork
{
    public IReservationRepository Reservations => reservations;
    public IParkingSessionRepository ParkingSessions => parkingSessions;
    public ISlotAllocationRepository SlotAllocations => slotAllocations;

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new Transaction(await db.Database.BeginTransactionAsync(cancellationToken));

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);

    private sealed class Transaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
