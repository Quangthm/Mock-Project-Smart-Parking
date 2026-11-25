using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class UnavailableReservationQuoteOwner : IReservationQuoteOwner
{
    public Task<PaymentReservationQuote> ReadProofAsync(Guid accountId, Guid quoteId, CancellationToken ct) =>
        throw new ReservationGateException(503, "PAYMENT_QUOTE_PROOF_UNAVAILABLE", "Payment quote owner is unavailable.");
}
