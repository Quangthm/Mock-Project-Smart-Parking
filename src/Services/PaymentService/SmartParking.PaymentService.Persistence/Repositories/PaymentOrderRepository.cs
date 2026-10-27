using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

public class PaymentOrderRepository : IPaymentOrderRepository
{
    private readonly PaymentDbContext _dbContext;

    public PaymentOrderRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentOrders
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PaymentOrder?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentOrders
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.OrderCode == orderCode, cancellationToken);
    }

    public async Task<PaymentOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentOrders
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task AddAsync(PaymentOrder order, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentOrders.AddAsync(order, cancellationToken);
    }

    public void Update(PaymentOrder order)
    {
        _dbContext.PaymentOrders.Update(order);
    }
}
