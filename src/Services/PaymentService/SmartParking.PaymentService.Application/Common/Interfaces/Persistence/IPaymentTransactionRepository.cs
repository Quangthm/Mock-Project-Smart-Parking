using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IPaymentTransactionRepository
{
    Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetByGatewayTransactionIdAsync(string gatewayTxId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentTransaction>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
    void Update(PaymentTransaction transaction);
}
