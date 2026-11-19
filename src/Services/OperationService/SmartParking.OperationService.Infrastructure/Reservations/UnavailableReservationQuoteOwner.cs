using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class UnavailableReservationQuoteOwner : IReservationQuoteOwner
{
    private static ReservationGateException Unavailable() =>
        new(503, "PAYMENT_QUOTE_PROOF_UNAVAILABLE", "Authoritative Payment evidence is unavailable.");

    public Task<PaymentReservationQuote> ReadProofAsync(Guid accountId, Guid quoteId, string version, CancellationToken ct) =>
        throw Unavailable();
    public Task<DepositOrderResult> EnsureOrderAsync(DepositOrderRequest request, CancellationToken ct) => throw Unavailable();
    public Task<ZeroOrderDecision> AcceptZeroAsync(ZeroOrderBinding binding, CancellationToken ct) => throw Unavailable();
    public Task ReconcileZeroAsync(ZeroOrderEvidence evidence, string reason, CancellationToken ct) => throw Unavailable();
}
