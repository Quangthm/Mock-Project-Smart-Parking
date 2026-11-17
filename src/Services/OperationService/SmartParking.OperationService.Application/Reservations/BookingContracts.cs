using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

internal static class BookingContracts
{
    public static BookingSelection Normalize(BookingSelection selection) => selection with
    { Start = selection.Start.ToUniversalTime(), End = selection.End.ToUniversalTime() };
    public static BookingSelection Selection(CreateReservationRequest input) => Normalize(new(input.VehicleId,
        input.SiteId, input.RequestedSlotId, input.Start, input.End, input.PaymentMethod));
    public static string Fingerprint(BookingSelection input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Normalize(input)))));
    public static ReservationGateException InvalidOwner() => new(503, "OWNER_CONTRACT_INVALID", "The owner response does not match the reservation contract.");
    public static BookingSelection Selection(ReservationRow row) => new(row.VehicleId!.Value, row.SiteId,
        row.TargetSlotId!.Value, row.ExpectedStartTime, row.ExpectedEndTime, row.SelectedPaymentMethod!);
    public static CommercialTerms AcceptedTerms(ReservationRow row)
    {
        var quote = JsonSerializer.Deserialize<CommercialTerms>(row.AcceptedPricingSnapshot!)
            ?? throw new InvalidOperationException("Stored accepted pricing terms are invalid.");
        if (quote.AccountId != row.AccountId || quote.Selection != Selection(row) ||
            quote.QuoteId == Guid.Empty || string.IsNullOrWhiteSpace(quote.Version) ||
            quote.BookedAmount != row.BookedAmount || quote.DepositAmount != row.DepositAmount ||
            quote.Currency != row.Currency || string.IsNullOrWhiteSpace(quote.CoverageVersion) ||
            string.IsNullOrWhiteSpace(quote.Disclosure))
            throw InvalidOwner();
        return quote;
    }
    public static void ValidateFacility(FacilityBookingFacts facts, BookingSelection intent)
    {
        if (facts.TenantId == Guid.Empty || facts.SiteId != intent.SiteId || facts.SlotId != intent.RequestedSlotId ||
            string.IsNullOrWhiteSpace(facts.Version) || !facts.ParticipatesInStructureGuard) throw InvalidOwner();
        if (!facts.EligibleCarSlot) throw new ReservationGateException(409, "SLOT_INELIGIBLE", "The slot is not eligible for this booking.");
    }
    public static void ValidateQuote(CommercialTerms quote, Guid account, BookingSelection intent, DateTimeOffset now)
    {
        if (quote.AccountId != account || quote.Selection != intent || quote.QuoteId == Guid.Empty ||
            string.IsNullOrWhiteSpace(quote.Version) || quote.ValidUntil <= now || quote.Currency != "VND" ||
            quote.BookedAmount < 0 || quote.DepositAmount < 0 || decimal.Truncate(quote.BookedAmount) != quote.BookedAmount ||
            decimal.Truncate(quote.DepositAmount) != quote.DepositAmount || quote.BookedAmount > 0 && quote.DepositAmount <= 0 ||
            quote.BookedAmount == 0 && quote.DepositAmount != 0 ||
            quote.PrepaymentMode is not ("FULL" or "PERCENTAGE" or "FIXED") ||
            quote.PrepaymentMode == "FULL" && quote.DepositAmount != quote.BookedAmount ||
            quote.PrepaymentMode == "PERCENTAGE" && quote.DepositAmount > quote.BookedAmount ||
            string.IsNullOrWhiteSpace(quote.CoverageVersion) || string.IsNullOrWhiteSpace(quote.Disclosure)) throw InvalidOwner();
    }
    public static void ValidateReservationCollectionBinding(ReservationRow row, ReservationDepositEvidence payment)
    {
        if (row.Id != payment.ReservationId || row.RequestId != payment.OperationId ||
            row.AccountId != payment.AccountId || row.TenantId != payment.TenantId || row.SiteId != payment.SiteId ||
            row.PaymentOrderId != payment.PaymentOrderId || row.DepositAmount is null or <= 0 ||
            row.DepositAmount != payment.Amount || row.Currency != payment.Currency)
            throw new InvalidOperationException("Payment collection is not bound to the reservation and order identity.");
        var accepted = AcceptedTerms(row);
        if (accepted.QuoteId != payment.QuoteId || accepted.Version != payment.QuoteVersion ||
            row.TermsId != payment.TermsId || row.TermsVersion != payment.TermsVersion)
            throw new InvalidOperationException("Payment collection is not bound to the accepted quote and terms.");
        if (row.CollectionId is not null && row.CollectionId != payment.CollectionId)
            throw new InvalidOperationException("A different payment collection is already recorded for this reservation.");
    }
    public static BookingView View(ReservationRow row) => new(row.Id, row.Status, row.SiteId, row.TargetSlotId,
        row.ExpectedStartTime, row.ExpectedEndTime, row.HoldExpiresAt, row.PaymentOrderId,
        row.OrderProvisioningState, row.OrderProvisioningCode,
        row.ZeroPriceAcceptanceState,
        row.BookedAmount, row.DepositAmount, row.Currency, row.ConfirmedAt, row.CreatedAt)
    {
        Vehicle = string.IsNullOrWhiteSpace(row.VehicleType) ? null : new(row.VehicleType, row.OriginalPlate ?? row.NormalizedPlate),
        LicensePlate = row.OriginalPlate ?? row.NormalizedPlate
    };

    public static BookingView View(BookingReadResult result) => View(result.Reservation) with
    {
        AllocatedSlotId = result.AllocatedSlotId,
        AllocationStatus = result.AllocationStatus,
        ParkingSessionId = result.ParkingSessionId
    };
}
