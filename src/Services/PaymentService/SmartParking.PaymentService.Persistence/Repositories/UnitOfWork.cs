using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Persistence.Context;

namespace SmartParking.PaymentService.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext _dbContext;

    public UnitOfWork(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
