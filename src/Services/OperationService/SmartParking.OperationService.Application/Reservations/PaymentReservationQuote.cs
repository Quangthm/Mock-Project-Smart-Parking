using System.Text.Json.Serialization;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record PaymentReservationQuote(
    Guid QuoteId, string QuoteVersion, string Status, DateTimeOffset ValidUntil, ReservationQuoteBinding Binding,
    string TariffVersion, [property: JsonRequired] decimal BookedAmount,
    [property: JsonRequired] decimal CoveredAmount, [property: JsonRequired] decimal DepositAmount,
    string Currency, string PrepaymentMode, string Disclosure)
{
    public void ValidateFor(ReservationQuoteBinding expected, DateTimeOffset now)
    {
        if (ValidUntil == default || QuoteId == Guid.Empty || string.IsNullOrWhiteSpace(QuoteVersion) || Binding != expected ||
            string.IsNullOrWhiteSpace(TariffVersion) || string.IsNullOrWhiteSpace(Disclosure) ||
            expected.Coverage.IsCovered || CoveredAmount != 0 || Currency != "VND" ||
            BookedAmount < 0 || decimal.Truncate(BookedAmount) != BookedAmount ||
            PrepaymentMode != "FULL" || DepositAmount != BookedAmount)
            throw BookingContracts.InvalidOwner();
        if (Status is not ("ISSUED" or "ACCEPTED" or "EXPIRED")) throw BookingContracts.InvalidOwner();
        if (Status != "ISSUED" || ValidUntil <= now)
            throw new ReservationGateException(409, "QUOTE_STALE", "Review the selection again to obtain a current quote.");
    }
}
