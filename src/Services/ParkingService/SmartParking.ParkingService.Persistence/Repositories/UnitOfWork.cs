using Microsoft.EntityFrameworkCore;
using SmartParking.ParkingService.Application.Common.Interfaces.Persistence;

namespace SmartParking.ParkingService.Persistence.Repositories;

public sealed class UnitOfWork(ParkingDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        new Transaction(await db.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Transaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
        : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
