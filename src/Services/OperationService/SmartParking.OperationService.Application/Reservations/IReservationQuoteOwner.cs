namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationQuoteOwner
{
    Task<PaymentReservationQuote> ReadProofAsync(Guid accountId, Guid quoteId, CancellationToken ct);
}
