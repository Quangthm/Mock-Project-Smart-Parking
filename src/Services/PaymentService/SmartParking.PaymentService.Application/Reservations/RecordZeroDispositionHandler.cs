using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Reservations;

public sealed class RecordZeroDispositionHandler(IPaymentOrderRepository orders, IUnitOfWork work)
    : IRequestHandler<RecordZeroDisposition, Guid>
{
    public Task<Guid> Handle(RecordZeroDisposition request, CancellationToken ct) =>
        work.WithLocksAsync([request.OperationId], async () =>
        {
            var order = await orders.GetByIdempotencyKeyAsync(request.OperationId.ToString(), ct);
            if (order?.ReviewedMethod == null || order.FinalAmount != 0 || order.ReferenceId != request.ReservationId ||
                order.AccountId != request.AccountId || order.Id != request.OrderId || order.Invoice?.Id != request.ReceiptId ||
                order.QuoteId != request.QuoteId || order.TermsVersion != request.QuoteVersion ||
                string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 100)
                throw new PaymentDomainException("QUOTE_BINDING_CONFLICT", "Disposition does not match a zero receipt.");
            order.ZeroDispositionReason ??= request.Reason;
            await work.SaveChangesAsync(ct);
            return order.Invoice.Id;
        }, ct);
}
