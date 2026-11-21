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
        var facts = await facility.ResolveSlotAsync(command.RequestedSlotId, ct);
        if (facts.SlotId != command.RequestedSlotId || facts.SiteId == Guid.Empty || facts.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(facts.Version) || !facts.ParticipatesInStructureGuard)
            throw BookingContracts.InvalidOwner();
        if (!facts.EligibleCarSlot)
            throw new ReservationGateException(409, "SLOT_INELIGIBLE", "The slot is ineligible for CAR reservations.");

        var now = clock.GetUtcNow();
        var policy = await mediator.Send(new ResolveReservationPoliciesQuery(facts.TenantId, facts.SiteId, now), ct);
        if (!policy.AllowsWindow(interval, now))
            throw new ReservationGateException(400, "BOOKING_WINDOW_INVALID", "The interval is outside the effective booking policy.");

        var coverage = subscriptions.Evaluate(plate, facts.TenantId, facts.SiteId, interval);
        var binding = new ReservationQuoteBinding(account, plate, facts.TenantId, facts.SiteId, facts.SlotId,
            interval.Start, interval.End, command.PaymentMethod, coverage, facts.Version,
            policy.Platform.Id, policy.Platform.Version, policy.Site.Id, policy.Site.Version);
        var quote = await payment.IssuePlateQuoteAsync(binding, ct);
        now = clock.GetUtcNow();
        quote.ValidateFor(binding, now);
        if (!policy.AllowsWindow(interval, now))
            throw new ReservationGateException(409, "QUOTE_STALE", "The booking window has changed.");
        return new(quote.QuoteId, quote.QuoteVersion, quote.ValidUntil, plate, facts.SiteId, facts.SlotId,
            interval.Start, interval.End, command.PaymentMethod, quote.TariffVersion, coverage.IsCovered, coverage.Version,
            quote.BookedAmount, quote.CoveredAmount, quote.DepositAmount, quote.Currency, quote.Disclosure,
            new(policy.Platform.Id, policy.Platform.Version, policy.Site.Id, policy.Site.Version,
                policy.Values, policy.ArrivalLateToleranceEnabled, policy.CancellationTimezone));
    }
}
