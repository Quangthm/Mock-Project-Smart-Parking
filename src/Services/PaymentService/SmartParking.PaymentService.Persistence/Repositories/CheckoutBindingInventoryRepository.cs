using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Persistence.Repositories;

public sealed class CheckoutBindingInventoryRepository(PaymentDbContext db) : ICheckoutBindingInventoryRepository
{
    public async Task<IReadOnlyList<CheckoutBindingCandidate>> ListAsync(int offset, int limit, CancellationToken ct) =>
        await db.PaymentTransactions.AsNoTracking()
            .Where(transaction => transaction.PaymentMethod == PaymentMethod.PAYOS && transaction.TransactionType == TransactionType.PAYMENT &&
                transaction.GatewayTransactionId != null && transaction.CheckoutUrl != null && transaction.CheckoutUrl != "" &&
                !db.Set<CheckoutSigningBinding>().Any(binding => binding.PaymentOrderId == transaction.PaymentOrderId))
            .OrderBy(transaction => transaction.PaymentOrderId).ThenBy(transaction => transaction.Id)
            .Skip(offset).Take(limit)
            .Select(transaction => new CheckoutBindingCandidate(transaction.PaymentOrderId, transaction.PaymentOrder!.OrderCode, transaction.GatewayTransactionId!))
            .ToListAsync(ct);
}
