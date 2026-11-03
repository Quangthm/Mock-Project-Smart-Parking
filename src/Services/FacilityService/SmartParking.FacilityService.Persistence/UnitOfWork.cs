using Microsoft.EntityFrameworkCore.Storage;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Persistence;

public sealed class UnitOfWork(ParkingDbContext database) : IUnitOfWork
{
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default) =>
        new Transaction(await database.Database.BeginTransactionAsync(cancellationToken));

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        database.SaveChangesAsync(cancellationToken);

    private sealed class Transaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
