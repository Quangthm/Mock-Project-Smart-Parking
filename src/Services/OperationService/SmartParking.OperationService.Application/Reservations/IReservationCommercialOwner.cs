namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationCommercialOwner
{
    Task<PaymentReservationQuote> IssuePlateQuoteAsync(ReservationQuoteBinding binding, CancellationToken ct);
    Task<PricePreview> PreviewAsync(Guid accountId, ReservationPricePreview intent, CancellationToken ct);
    Task<CommercialTerms> QuoteAsync(Guid accountId, Guid tenantId, BookingSelection intent, CancellationToken ct);
    // A real Payment order is created only after the reservation hold is durably committed.
    // Quote review remains local to the accepted booking snapshot; subsequent order creation is
    // idempotent and uses the reservation/order operation identity rather than a separate accept step.
    Task<DepositOrderResult> EnsureOrderAsync(DepositOrderRequest request, CancellationToken ct);
    Task<ZeroPriceAcceptanceResult> AcceptZeroPriceAsync(ZeroPriceAcceptanceRequest request, CancellationToken ct);
    Task<CollectionLookup> CollectionsAsync(Guid reservationId, Guid orderId, CancellationToken ct);
    // Durable, idempotent owner acceptance; not proof of refund payout.
    Task ReconcileAsync(Guid reservationId, VerifiedCollection collection, string reason, CancellationToken ct);
}
