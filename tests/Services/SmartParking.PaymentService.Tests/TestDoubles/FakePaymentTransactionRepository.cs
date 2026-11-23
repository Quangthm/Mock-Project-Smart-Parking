using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Tests.TestDoubles;

public class FakePaymentTransactionRepository : IPaymentTransactionRepository
{
    public readonly List<PaymentTransaction> Transactions = new();

    public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Transactions.FirstOrDefault(x => x.Id == id));

    public Task<PaymentTransaction?> GetByGatewayTransactionIdAsync(string gatewayTxId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Transactions.FirstOrDefault(x => x.GatewayTransactionId == gatewayTxId));

    public Task<PaymentTransaction?> GetByProviderCollectionReferenceAsync(PaymentMethod method, string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(Transactions.SingleOrDefault(x => x.PaymentMethod == method && x.ProviderCollectionReference == reference));

    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(Transactions.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));

    public Task<IReadOnlyList<PaymentTransaction>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PaymentTransaction>>(Transactions.Where(x => x.PaymentOrderId == orderId).ToList());

    public Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        Transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public void Update(PaymentTransaction transaction)
    {
        var index = Transactions.FindIndex(x => x.Id == transaction.Id);
        if (index >= 0) Transactions[index] = transaction;
        else Transactions.Add(transaction);
    }
}
