using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Reservations;

public sealed class RecordZeroDispositionHandler(IPaymentOrderRepository orders, IUnitOfWork work)
    : IRequestHandler<RecordZeroDisposition, Guid>
{
    public Task<Guid> Handle(RecordZeroDisposition request, CancellationToken ct) =>
        work.WithLocksAsync([request.OperationId], async () =>
        {
            var order = await orders.GetByIdempotencyKeyAsync(request.OperationId.ToString(), ct);
            if (order?.ReviewedMethod == null || order.IsProvisioningFence || order.Status != PaymentOrderStatus.PAID ||
                order.OrderType != OrderType.RESERVATION_FEE || order.ReferenceType != ReferenceType.RESERVATION ||
                order.FinalAmount != 0 || order.ReferenceId != request.ReservationId ||
                order.AccountId != request.AccountId || order.Id != request.OrderId ||
                order.QuoteId != request.QuoteId || order.TermsVersion != request.QuoteVersion ||
                order.Invoice is not { } receipt || request.ReceiptId == Guid.Empty || receipt.Id != request.ReceiptId ||
                receipt.PaymentOrderId != order.Id || receipt.ReferenceType != ReferenceType.RESERVATION ||
                receipt.TenantId != order.TenantId || receipt.SiteId != order.SiteId ||
                receipt.ReferenceId != request.ReservationId || receipt.FinalAmount != 0 ||
                !receipt.PricingSnapshot.IsSettled || receipt.PricingSnapshot.AccountId != order.AccountId ||
                receipt.PricingSnapshot.Currency != order.ReviewedCurrency ||
                string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 100)
                throw new PaymentDomainException("QUOTE_BINDING_CONFLICT", "Disposition does not match a zero receipt.");
            order.ZeroDispositionReason ??= request.Reason;
            await work.SaveChangesAsync(ct);
            return receipt.Id;
        }, ct);
}
