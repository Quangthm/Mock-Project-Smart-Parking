using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface ISubscriptionClosureCompensationRepository
{
    Task<IPaymentDbTransaction?> BeginTransactionAsync(CancellationToken ct = default);
    Task<SubscriptionClosureCompensation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<PaymentOrder?> GetOrderForUpdateAsync(Guid paymentOrderId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionClosureCompensation>> GetByOrderIdAsync(Guid paymentOrderId, CancellationToken ct = default);
    Task<decimal> GetNonRejectedRefundRequestsTotalAsync(Guid paymentOrderId, CancellationToken ct = default);
    Task AddCompensationAsync(SubscriptionClosureCompensation compensation, CancellationToken ct = default);
    Task AddRefundRequestAsync(RefundRequest refundRequest, CancellationToken ct = default);
    Task AddRefundTransactionAsync(PaymentTransaction transaction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
