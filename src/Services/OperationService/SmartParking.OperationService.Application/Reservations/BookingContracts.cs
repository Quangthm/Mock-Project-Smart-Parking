using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

internal static class BookingContracts
{
    // Existing vehicle-based bookings still use this binding for confirmation replay.
    public static string Fingerprint(BookingSelection selection) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(selection with
        { Start = selection.Start.ToUniversalTime(), End = selection.End.ToUniversalTime() }))));

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
    public static void ValidateReservationCollectionBinding(ReservationRow row, ReservationDepositEvidence payment)
    {
        if (row.Id != payment.ReservationId || row.RequestId != payment.OperationId ||
            row.AccountId != payment.AccountId || row.TenantId != payment.TenantId || row.SiteId != payment.SiteId ||
            row.PaymentOrderId != payment.PaymentOrderId || row.DepositAmount is null or <= 0 ||
            row.DepositAmount != payment.Amount || row.Currency != payment.Currency)
            throw new InvalidOperationException("Payment collection is not bound to the reservation and order identity.");
        var legacy = row.QuoteId is null ? AcceptedTerms(row) : null;
        if ((row.QuoteId ?? legacy!.QuoteId) != payment.QuoteId || (row.QuoteVersion ?? legacy!.Version) != payment.QuoteVersion ||
            row.QuoteId.HasValue && (payment.PaymentMethod != row.SelectedPaymentMethod ||
                string.IsNullOrWhiteSpace(payment.Provider) || string.IsNullOrWhiteSpace(payment.ProviderReference) ||
                payment.ProviderVerifiedAt == default || payment.OccurredAt == default) ||
            row.TermsId != payment.TermsId || row.TermsVersion != payment.TermsVersion)
            throw new InvalidOperationException("Payment collection is not bound to the accepted quote and terms.");
        if (row.CollectionId is not null && row.CollectionId != payment.CollectionId)
            throw new InvalidOperationException("A different payment collection is already recorded for this reservation.");
    }
    public static ZeroOrderBinding ZeroBinding(ReservationRow row) => new(row.RequestId!.Value, row.Id,
        row.AccountId, row.TenantId, row.SiteId, row.TargetSlotId!.Value, row.NormalizedPlate!, row.ExpectedStartTime,
        row.ExpectedEndTime, row.SelectedPaymentMethod!, row.QuoteId!.Value, row.QuoteVersion!, row.IsCovered!.Value, row.CoverageVersion!,
        row.Currency!, row.DepositAmount!.Value, row.HoldExpiresAt!.Value);

    public static BookingView View(ReservationRow row) => new(row.Id, row.Status, row.SiteId, row.TargetSlotId,
        row.ExpectedStartTime, row.ExpectedEndTime, row.HoldExpiresAt, row.PaymentOrderId,
        row.OrderProvisioningState, row.OrderProvisioningCode,
        row.ZeroPriceAcceptanceState,
        row.BookedAmount, row.DepositAmount, row.Currency, row.ConfirmedAt, row.CreatedAt)
    {
        RequestId = row.RequestId,
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
