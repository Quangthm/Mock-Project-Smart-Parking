using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class BookingSelectionValidator : AbstractValidator<BookingSelection>
{
    public BookingSelectionValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty(); RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.RequestedSlotId).NotEmpty(); RuleFor(x => x.End).GreaterThan(x => x.Start);
        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(32);
    }
}
public sealed class ReviewReservationValidator : AbstractValidator<ReviewReservationTermsCommand>
{
    public ReviewReservationValidator() { RuleFor(x => x.Intent).NotNull().SetValidator(new BookingSelectionValidator()); }
}
public sealed class PreviewReservationValidator : AbstractValidator<PreviewReservationPriceQuery>
{
    public PreviewReservationValidator()
    {
        RuleFor(x => x.Intent).NotNull();
        When(x => x.Intent is not null, () =>
        {
            RuleFor(x => x.Intent.SiteId).NotEmpty(); RuleFor(x => x.Intent.RequestedSlotId).NotEmpty();
            RuleFor(x => x.Intent.End).GreaterThan(x => x.Intent.Start);
            RuleFor(x => x.Intent.VehicleId).Must(x => x is null || x != Guid.Empty);
            RuleFor(x => x.Intent.PaymentMethod).MaximumLength(32);
        });
    }
}
public sealed class PreviewReservationPriceHandler(ICurrentActor actor, IReservationCommercialOwner commercial)
    : IRequestHandler<PreviewReservationPriceQuery, PricePreview>
{
    public async Task<PricePreview> Handle(PreviewReservationPriceQuery query, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        var result = await commercial.PreviewAsync(account, query.Intent with
        { Start = query.Intent.Start.ToUniversalTime(), End = query.Intent.End.ToUniversalTime() }, ct);
        if (result.BookedAmount < 0 || result.DepositAmount < 0 || result.Currency != "VND" || string.IsNullOrWhiteSpace(result.PricingVersion))
            throw BookingContracts.InvalidOwner();
        return result;
    }
}
public sealed class ReviewReservationTermsHandler(ICurrentActor actor, IReservationCommercialOwner commercial,
    IReservationFacilityOwner facility, IReservationTermsRepository terms, IReservationBookingRepository bookings,
    IMediator mediator, IUnitOfWork work) : IRequestHandler<ReviewReservationTermsCommand, ReviewedTerms>
{
    public async Task<ReviewedTerms> Handle(ReviewReservationTermsCommand command, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        var intent = BookingContracts.Normalize(command.Intent);
        var facts = await facility.ReadAsync(intent.SiteId, intent.RequestedSlotId, ct);
        BookingContracts.ValidateFacility(facts, intent);
        var now = await bookings.DatabaseNowAsync(ct);
        var policy = await mediator.Send(new ResolveReservationPoliciesQuery(facts.TenantId, facts.SiteId, now), ct);
        if (!policy.AllowsWindow(new ReservationInterval(intent.Start, intent.End), now))
            throw new ReservationGateException(400, "BOOKING_WINDOW_INVALID", "The requested interval is outside the effective booking policy.");
        // Preview/review does not evaluate vehicle authority. That happens once after workflow Allowed.
        var quote = await commercial.QuoteAsync(account, facts.TenantId, intent, ct);
        BookingContracts.ValidateQuote(quote, account, intent, await bookings.DatabaseNowAsync(ct));
        var snapshot = new BookingTermsSnapshot(quote, facts, JsonSerializer.Serialize(policy),
            policy.Platform.Id, policy.Platform.Version, policy.Site.Id, policy.Site.Version);
        var row = ReservationTerms.Issue(Guid.NewGuid(), account, BookingContracts.Fingerprint(intent),
            quote.Version, quote.ValidUntil, JsonSerializer.Serialize(snapshot));
        terms.Add(row);
        await work.SaveChangesAsync(ct);
        return new(row.Id, row.Version, row.ValidUntil, quote,
            new(policy.Platform.Id, policy.Platform.Version, policy.Site.Id, policy.Site.Version,
                policy.Values, policy.ArrivalLateToleranceEnabled, policy.CancellationTimezone));
    }
}
