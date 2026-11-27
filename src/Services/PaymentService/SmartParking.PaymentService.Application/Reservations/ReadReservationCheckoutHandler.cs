using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Reservations;

public sealed class ReadReservationCheckoutHandler(IPaymentOrderRepository orders, IUnitOfWork work)
    : IRequestHandler<ReadReservationCheckout, ReservationCheckoutState>
{
    public async Task<ReservationCheckoutState> Handle(ReadReservationCheckout query, CancellationToken ct)
    {
        if (query.AccountId == Guid.Empty)
            throw new PaymentDomainException("FORBIDDEN", "Driver access is required.");
        if (query.ReservationId.HasValue == query.OrderId.HasValue)
            throw new ArgumentException("Exactly one checkout identifier is required.");
        var order = await orders.FindOwnCheckoutAsync(query.AccountId, query.ReservationId, query.OrderId, ct);
        if (order is null) return new("MISSING", null, null);
        if (order.ReferenceType != ReferenceType.RESERVATION)
            return query.OrderId.HasValue ? new("STANDALONE", null, order.Id) : new("MISSING", null, null);
        if (order.ReviewedMethod == null) return new("HISTORICAL", order.ReferenceId, order.Id);
        if (query.ReservationId.HasValue && order.ReferenceId != query.ReservationId)
            throw new InvalidOperationException("Reservation order binding is inconsistent.");
        if (order.IsProvisioningFence) return new("CLOSED", order.ReferenceId, null);
        var state = order.Status switch
        {
            PaymentOrderStatus.PAID => "PAID",
            PaymentOrderStatus.FAILED => "FAILED",
            PaymentOrderStatus.CANCELLED or PaymentOrderStatus.REFUNDED => "CLOSED",
            PaymentOrderStatus.PENDING when order.FinalAmount > 0 && order.ExpiresAt.HasValue =>
                await work.DatabaseNowAsync(ct) < order.ExpiresAt ? "PAYABLE" : "CLOSED",
            _ => throw new InvalidOperationException("Reservation order has inconsistent checkout state.")
        };
        return new(state, order.ReferenceId, order.Id, order.ExpiresAt);
    }
}
