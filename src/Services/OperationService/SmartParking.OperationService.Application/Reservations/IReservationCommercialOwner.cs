namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationCommercialOwner
{
    Task<PaymentReservationQuote> IssuePlateQuoteAsync(ReservationQuoteBinding binding, CancellationToken ct);
    Task<PricePreview> PreviewAsync(Guid accountId, ReservationPricePreview intent, CancellationToken ct);
    Task<CollectionLookup> CollectionsAsync(Guid reservationId, Guid orderId, CancellationToken ct);
    // Durable, idempotent owner acceptance; not proof of refund payout.
    Task ReconcileAsync(Guid reservationId, VerifiedCollection collection, string reason, CancellationToken ct);
}
