using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

namespace SmartParking.PaymentService.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext _dbContext;

    public UnitOfWork(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> WithLocksAsync<T>(Guid[] identities, Func<Task<T>> action, CancellationToken ct)
    {
        await using var ownedTransaction = _dbContext.Database.CurrentTransaction is null
            ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        foreach (var id in identities.Distinct().Order())
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 273))", ct);
        var result = await action();
        if (ownedTransaction is not null) await ownedTransaction.CommitAsync(ct);
        return result;
    }

    public Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct) =>
        _dbContext.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
