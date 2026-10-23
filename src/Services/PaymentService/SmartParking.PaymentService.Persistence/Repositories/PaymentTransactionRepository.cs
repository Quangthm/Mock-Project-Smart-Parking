using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Persistence.Context;

namespace SmartParking.PaymentService.Persistence.Repositories;

public class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly PaymentDbContext _dbContext;

    public PaymentTransactionRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentTransactions
            .Include(x => x.PaymentOrder)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PaymentTransaction?> GetByGatewayTransactionIdAsync(string gatewayTxId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentTransactions
            .Include(x => x.PaymentOrder)
            .FirstOrDefaultAsync(x => x.GatewayTransactionId == gatewayTxId, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTransaction>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentTransactions
            .Where(x => x.PaymentOrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentTransactions.AddAsync(transaction, cancellationToken);
    }

    public void Update(PaymentTransaction transaction)
    {
        _dbContext.PaymentTransactions.Update(transaction);
    }
}
