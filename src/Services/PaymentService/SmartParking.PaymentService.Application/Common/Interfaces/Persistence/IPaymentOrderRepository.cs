using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IPaymentOrderRepository
{
    Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentOrder?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<PaymentOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task AddAsync(PaymentOrder order, CancellationToken cancellationToken = default);
    Task<Guid?> FindReviewedOrderIdAsync(string orderCode, CancellationToken ct);
    Task<bool> HasReviewedReservationAsync(Guid reservationId, CancellationToken ct);
    void Update(PaymentOrder order);
}
