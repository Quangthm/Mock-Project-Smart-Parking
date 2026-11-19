using MediatR;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record BookingSelection(Guid VehicleId, Guid SiteId, Guid RequestedSlotId,
    DateTimeOffset Start, DateTimeOffset End, string PaymentMethod);
public sealed record ReservationPricePreview(Guid SiteId, Guid RequestedSlotId,
    DateTimeOffset Start, DateTimeOffset End, Guid? VehicleId, string? PaymentMethod);
public sealed record PricePreview(decimal BookedAmount, decimal? DepositAmount, string Currency,
    string PricingVersion, bool RequiresCompleteSelection);
public sealed record CommercialTerms(Guid QuoteId, string Version, DateTimeOffset ValidUntil,
    BookingSelection Selection, Guid AccountId, decimal BookedAmount, decimal DepositAmount,
    string Currency, string PrepaymentMode, string CoverageVersion, string Disclosure);
public sealed record BookingTermsSnapshot(CommercialTerms Commercial, FacilityBookingFacts Facility,
    string PolicyJson, Guid PlatformPolicyId, int PlatformPolicyVersion, Guid SitePolicyId, int SitePolicyVersion);
public sealed record ReviewedTerms(Guid TermsId, string TermsVersion, DateTimeOffset ValidUntil,
    CommercialTerms Commercial, ReviewedOperationalPolicy OperationalPolicy);
public sealed record VehicleBindingResult(bool Eligible, long? Revision, string? CanonicalPlate);
public sealed record ZeroPriceAcceptanceResult(string State, ZeroPriceAcceptanceRequest AcceptedRequest,
    string? OutcomeCode = null);
// CREATED replays the same order. CLOSED_NO_ORDER is an authoritative, durable fence
// against later creation for this operation. UNKNOWN must never be translated into absence.
public sealed record DepositOrderResult(string State, Guid? OrderId, DepositOrderRequest AcceptedRequest,
    string? OutcomeCode = null);
public sealed record VerifiedCollection(Guid CollectionId, Guid OrderId, Guid ReservationId, Guid AccountId,
    Guid TenantId, Guid SiteId, Guid QuoteId, string TermsVersion, decimal Amount, string Currency);
public sealed record CollectionLookup(string State, IReadOnlyList<VerifiedCollection> Collections);
public sealed record VerifiedReservationCollection(Guid EventId, Guid CollectionId, Guid PaymentOrderId,
    Guid PaymentTransactionId, Guid OperationId, Guid ReservationId, Guid AccountId, Guid TenantId, Guid SiteId,
    Guid QuoteId, string QuoteVersion, Guid TermsId, string TermsVersion, decimal Amount, string Currency,
    string Provider, string ProviderReference, DateTimeOffset ProviderVerifiedAt, DateTimeOffset OccurredAt);
public sealed record ReservationPaymentEvidence(Guid EventId, Guid PaymentOrderId, Guid PaymentTransactionId,
    Guid ReservationId, Guid AccountId, Guid TenantId, Guid SiteId, decimal Amount, string Currency,
    string Provider, string? ProviderReference, DateTimeOffset PaidAt);
public sealed record ReservationConfirmedV1(Guid EventId, Guid ReservationId, Guid AccountId,
    Guid TenantId, Guid SiteId, DateTimeOffset ConfirmedAt);

// These are owner boundaries, not services forwarding Application workflow logic.
// Owners implement authenticated/versioned transports; unavailable never means free/no coverage.
public interface IReservationVehicleOwner
{
    Task<VehicleBindingResult> ResolveAsync(Guid accountId, Guid vehicleId, CancellationToken ct);
}
public interface IReservationFactsPublisher
{
    Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct);
    Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct);
    Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct);
    Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct);
}
public interface IReservationTermsRepository
{
    void Add(ReservationTerms terms);
    Task<ReservationTerms?> LockAsync(Guid id, CancellationToken ct);
}

public sealed record PreviewReservationPriceQuery(ReservationPricePreview Intent) : IRequest<PricePreview>;
public sealed record ReviewReservationTermsCommand(BookingSelection Intent) : IRequest<ReviewedTerms>;
public sealed record ProgressReservationCommand(Guid RequestId) : IRequest;
public sealed record RecordReservationPaymentCommand(ReservationPaymentEvidence Evidence) : IRequest;
