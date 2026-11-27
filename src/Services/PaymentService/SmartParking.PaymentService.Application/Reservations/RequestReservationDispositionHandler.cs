using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.ReconcileReservation;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed class RequestReservationDispositionHandler(IPaymentOrderRepository orders, IMediator mediator, IUnitOfWork work)
    : IRequestHandler<RequestReservationDisposition>
{
    public Task Handle(RequestReservationDisposition command, CancellationToken ct)
    {
        var e = command.Evidence;
        return work.WithLocksAsync([e.ReservationId], async () =>
        {
            var order = await orders.GetByIdAsync(e.PaymentOrderId, ct);
            if (order is null || e.ReservationId == Guid.Empty || order.ReferenceType != ReferenceType.RESERVATION ||
                order.ReviewedMethod == null || order.ReferenceId != e.ReservationId || order.AccountId != e.AccountId ||
                order.TenantId != e.TenantId || order.SiteId != e.SiteId || order.QuoteId != e.QuoteId ||
                order.FinalAmount != e.Amount || order.ReviewedCurrency != e.Currency || string.IsNullOrWhiteSpace(e.Reason))
                throw Conflict();
            if (e.CollectionId is Guid collection && e.ZeroReceiptId is null && e.Amount > 0 &&
                order.Transactions.Any(t => t.Id == collection && t.HasVerifiedProviderCollection()))
                await mediator.Send(new ReconcileReservationCollectionCommand(new(e.ReservationId, collection, e.Amount, e.Reason)), ct);
            else if (e.ZeroReceiptId is Guid receipt && e.CollectionId is null && e.Amount == 0)
                await mediator.Send(new RecordZeroDisposition(e.ReservationId, e.ReservationId, e.AccountId,
                    e.PaymentOrderId, receipt, e.QuoteId, e.Reason), ct);
            else throw Conflict();
            return order.Id;
        }, ct);
    }

    private static PaymentDomainException Conflict() => new("QUOTE_BINDING_CONFLICT", "Disposition does not match financial evidence.");
}
