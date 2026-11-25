using MediatR;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReviewReservationQuoteHandler(ICurrentActor actor, IReservationFacilityOwner facility,
    IReservationCommercialOwner payment, IReservationSubscriptionEligibility subscriptions,
    TimeProvider clock, IMediator mediator)
    : IRequestHandler<ReviewReservationQuoteCommand, ReviewedReservationQuote>
{
    public async Task<ReviewedReservationQuote> Handle(ReviewReservationQuoteCommand command, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        var plate = ReservationCarPlate.Canonicalize(command.LicensePlate)
            ?? throw new ReservationGateException(400, "INVALID_REQUEST", "Invalid CAR plate.");
        var interval = new ReservationInterval(command.Start, command.End);
        var metadata = await facility.ResolveSlotAsync(command.RequestedSlotId, ct);
        if (metadata.SlotId != command.RequestedSlotId || metadata.SiteId == Guid.Empty || metadata.TenantId == Guid.Empty || metadata.SlotId == Guid.Empty)
            throw BookingContracts.InvalidOwner();

        var now = clock.GetUtcNow();
        var policy = await mediator.Send(new ResolveReservationPoliciesQuery(metadata.TenantId, metadata.SiteId, now), ct);
        if (!policy.AllowsWindow(interval, now))
            throw new ReservationGateException(400, "BOOKING_WINDOW_INVALID", "The interval is outside the effective booking policy.");

        var coverage = subscriptions.Evaluate(plate, metadata.TenantId, metadata.SiteId, interval);
        var binding = new ReservationQuoteBinding(account, plate, metadata.TenantId, metadata.SiteId, metadata.SlotId,
            interval.Start, interval.End, command.PaymentMethod, coverage,
            policy.Platform.Id, policy.Platform.Version, policy.Site.Id, policy.Site.Version);
        var quote = await payment.IssuePlateQuoteAsync(binding, ct);
        now = clock.GetUtcNow();
        quote.ValidateFor(binding, now);
        if (!policy.AllowsWindow(interval, now))
            throw new ReservationGateException(409, "QUOTE_STALE", "The booking window has changed.");
        return new(quote.QuoteId, quote.QuoteVersion, quote.ValidUntil, plate, metadata.SiteId, metadata.SlotId,
            interval.Start, interval.End, command.PaymentMethod, quote.TariffVersion, coverage.IsCovered, coverage.Version,
            quote.BookedAmount, quote.CoveredAmount, quote.DepositAmount, quote.Currency, quote.Disclosure,
            new(policy.Platform.Id, policy.Platform.Version, policy.Site.Id, policy.Site.Version,
                policy.Values, policy.ArrivalLateToleranceEnabled, policy.CancellationTimezone));
    }
}
