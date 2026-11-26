using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IRefundRequestRepository
{
    Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefundRequest>> GetByPaymentOrderIdAsync(Guid paymentOrderId, CancellationToken cancellationToken = default);
    Task AddAsync(RefundRequest refundRequest, CancellationToken cancellationToken = default);
    void Update(RefundRequest refundRequest);
}
