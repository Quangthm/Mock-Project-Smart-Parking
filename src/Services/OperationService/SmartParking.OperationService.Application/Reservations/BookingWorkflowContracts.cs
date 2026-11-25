using MediatR;
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
public sealed record ZeroPriceAcceptanceResult(string State, ZeroPriceAcceptanceRequest AcceptedRequest,
    string? OutcomeCode = null);
public sealed record ReservationConfirmedV1(Guid EventId, Guid ReservationId, Guid AccountId,
    Guid TenantId, Guid SiteId, DateTimeOffset ConfirmedAt);

public interface IReservationFactsPublisher
{
    Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct);
    Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct);
    Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct);
    Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct);
}
public sealed record PreviewReservationPriceQuery(ReservationPricePreview Intent) : IRequest<PricePreview>;
