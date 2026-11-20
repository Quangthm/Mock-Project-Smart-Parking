using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Tests.TestDoubles;

public class FakePaymentOrderRepository : IPaymentOrderRepository
{
    public readonly List<PaymentOrder> Orders = new();

    public Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));
    }

    public Task<PaymentOrder?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Orders.FirstOrDefault(x => x.OrderCode == orderCode));
    }

    public Task<PaymentOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Orders.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));
    }

    public Task AddAsync(PaymentOrder order, CancellationToken cancellationToken = default)
    {
        Orders.Add(order);
        return Task.CompletedTask;
    }

    public Task<Guid?> FindReviewedOrderIdAsync(string orderCode, CancellationToken ct) =>
        Task.FromResult(Orders.SingleOrDefault(o => o.OrderCode == orderCode && o.ReviewedMethod != null)?.Id);

    public Task<bool> HasReviewedReservationAsync(Guid reservationId, CancellationToken ct) =>
        Task.FromResult(Orders.Any(o => o.ReferenceId == reservationId && o.ReviewedMethod != null));

    public void Update(PaymentOrder order)
    {
        var idx = Orders.FindIndex(x => x.Id == order.Id);
        if (idx >= 0) Orders[idx] = order;
        else Orders.Add(order);
    }
}
