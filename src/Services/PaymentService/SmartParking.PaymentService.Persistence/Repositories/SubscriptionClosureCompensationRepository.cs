using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

public sealed class SubscriptionClosureCompensationRepository(PaymentDbContext db) : ISubscriptionClosureCompensationRepository
{
    public async Task<IPaymentDbTransaction?> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (!db.Database.IsRelational())
        {
            return null;
        }

        var tx = await db.Database.BeginTransactionAsync(ct);
        return new PaymentDbTransactionWrapper(tx);
    }

    public Task<SubscriptionClosureCompensation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        db.SubscriptionClosureCompensations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

    public async Task<PaymentOrder?> GetOrderForUpdateAsync(Guid paymentOrderId, CancellationToken ct = default)
    {
        if (db.Database.IsRelational())
        {
            return await db.PaymentOrders
                .FromSqlInterpolated($"SELECT * FROM payment_orders WHERE id = {paymentOrderId} FOR UPDATE")
                .FirstOrDefaultAsync(ct);
        }

        return await db.PaymentOrders.FirstOrDefaultAsync(o => o.Id == paymentOrderId, ct);
    }

    public async Task<IReadOnlyList<SubscriptionClosureCompensation>> GetByOrderIdAsync(
        Guid paymentOrderId,
        CancellationToken ct = default) =>
        await db.SubscriptionClosureCompensations
            .AsNoTracking()
            .Where(x => x.PaymentOrderId == paymentOrderId)
            .OrderBy(x => x.CreatedAt)
            .ToArrayAsync(ct);

    public async Task<decimal> GetNonRejectedRefundRequestsTotalAsync(Guid paymentOrderId, CancellationToken ct = default)
    {
        var amounts = await db.RefundRequests
            .AsNoTracking()
            .Where(r => r.PaymentOrderId == paymentOrderId &&
                        r.Status != "REJECTED" &&
                        r.Status != "CANCELLED" &&
                        r.Status != "FAILED")
            .Select(r => r.Amount)
            .ToArrayAsync(ct);

        return amounts.Sum();
    }

    public async Task AddCompensationAsync(SubscriptionClosureCompensation compensation, CancellationToken ct = default) =>
        await db.SubscriptionClosureCompensations.AddAsync(compensation, ct);

    public async Task AddRefundRequestAsync(RefundRequest refundRequest, CancellationToken ct = default) =>
        await db.RefundRequests.AddAsync(refundRequest, ct);

    public async Task AddRefundTransactionAsync(PaymentTransaction transaction, CancellationToken ct = default) =>
        await db.PaymentTransactions.AddAsync(transaction, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
