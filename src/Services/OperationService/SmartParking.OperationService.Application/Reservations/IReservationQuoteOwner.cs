namespace SmartParking.OperationService.Application.Reservations;

/// <summary>Authenticated, immutable Payment evidence. The production adapter is gated until owner cutover.</summary>
public interface IReservationQuoteOwner
{
    // Only the original persisted issuance, including current consumption status, satisfies this read.
    Task<PaymentReservationQuote> ReadProofAsync(Guid accountId, Guid quoteId, string version, CancellationToken ct);
    Task<DepositOrderResult> EnsureOrderAsync(DepositOrderRequest request, CancellationToken ct);
    Task<ZeroOrderDecision> AcceptZeroAsync(ZeroOrderBinding binding, CancellationToken ct);
    Task ReconcileZeroAsync(ZeroOrderEvidence evidence, string reason, CancellationToken ct);
}
