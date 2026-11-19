using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class RecordReservationPaymentHandler(IReservationBookingRepository bookings, IMediator mediator)
    : IRequestHandler<RecordReservationPaymentCommand>
{
    public async Task Handle(RecordReservationPaymentCommand command, CancellationToken ct)
    {
        var payment = command.Evidence;
        if (payment.EventId == Guid.Empty || payment.PaymentOrderId == Guid.Empty ||
            payment.PaymentTransactionId == Guid.Empty || payment.ReservationId == Guid.Empty ||
            payment.AccountId == Guid.Empty || payment.TenantId == Guid.Empty || payment.SiteId == Guid.Empty)
            return;

        var observed = await bookings.FindOrderProvisioningAsync(payment.ReservationId, ct);
        // This legacy wire cannot independently attest quote/version, method or collection identity.
        if (observed is null || observed.QuoteId.HasValue) return;

        if (observed.RequestId is not { } operationId || observed.TermsId is not { } termsId ||
            string.IsNullOrWhiteSpace(observed.TermsVersion))
            throw new InvalidOperationException("Reservation payment evidence arrived before accepted terms were persisted.");

        if (observed.PaymentOrderId is null)
            throw new InvalidOperationException("Reservation payment evidence arrived before the order binding was persisted.");
        if (observed.PaymentOrderId != payment.PaymentOrderId)
            throw new InvalidOperationException("Reservation payment evidence references a different order.");

        if (observed.AccountId != payment.AccountId || observed.TenantId != payment.TenantId ||
            observed.SiteId != payment.SiteId || observed.DepositAmount is null or <= 0 ||
            observed.DepositAmount != payment.Amount ||
            !string.Equals(observed.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reservation payment evidence does not match the persisted booking binding.");

        var terms = BookingContracts.AcceptedTerms(observed);
        var collected = new ReservationDepositEvidence(payment.EventId, payment.PaymentTransactionId,
            payment.PaymentOrderId, payment.PaymentTransactionId, operationId, observed.Id, observed.AccountId,
            observed.TenantId, observed.SiteId, terms.QuoteId, terms.Version, termsId, observed.TermsVersion,
            payment.Amount, payment.Currency, payment.Provider, payment.ProviderReference ?? string.Empty,
            payment.PaidAt, payment.PaidAt);

        await mediator.Send(new RecordReservationDepositCommand(collected), ct);
    }
}
