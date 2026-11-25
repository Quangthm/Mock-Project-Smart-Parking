using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
namespace SmartParking.OperationService.Application.Reservations;

public sealed class LinkReservationZeroReceiptHandler(IMediator mediator, IUnitOfWork work,
    IReservationBookingRepository bookings, IReservationFactsPublisher facts) : IRequestHandler<LinkReservationZeroReceipt>
{
    public async Task Handle(LinkReservationZeroReceipt command, CancellationToken ct)
    {
        var e = command.Evidence;
        await using var tx = await work.BeginTransactionAsync(ct);
        var booking = await mediator.Send(new LockReservationBooking(e.ReservationId), ct);
        if (booking?.QuoteId is null || booking.QuoteId != e.QuoteId || booking.AccountId != e.AccountId ||
            booking.TenantId != e.TenantId || booking.SiteId != e.SiteId || booking.Currency != e.Currency ||
            e.DepositAmount != 0 || booking.DepositAmount != 0)
            throw new InvalidOperationException("Receipt conflicts with the accepted zero-deposit reservation.");
        if (!booking.LinkZeroReceipt(e.PaymentOrderId, e.ReceiptId)) return;
        var reason = booking.Status is "CANCELLED" or "INVALIDATED"
            ? await bookings.FindZeroDispositionReasonAsync(booking.Id, booking.AccountId, ct) : null;
        if (reason != null)
            await facts.RequestDispositionAsync(new(booking.Id, e.AccountId, e.TenantId, e.SiteId, e.QuoteId,
                e.PaymentOrderId, null, e.ReceiptId, 0, e.Currency, reason), ct);
        await work.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
