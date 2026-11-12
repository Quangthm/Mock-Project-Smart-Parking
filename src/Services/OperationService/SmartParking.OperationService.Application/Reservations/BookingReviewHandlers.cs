using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Domain.Entities;

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
public sealed class ReadOwnBookingHandler(ICurrentActor actor, IReservationBookingRepository bookings)
    : IRequestHandler<ReadOwnBookingQuery, BookingView>
{
    public async Task<BookingView> Handle(ReadOwnBookingQuery query, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        var row = await bookings.FindOwnBookingAsync(account, query.Id, ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found.");
        return BookingContracts.View(row);
    }
}
public sealed class ReadOwnBookingsValidator : AbstractValidator<ReadOwnBookingsQuery>
{
    public ReadOwnBookingsValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => (x.Before is null) == (x.BeforeId is null));
        RuleFor(x => x.BeforeId).Must(x => x is null || x != Guid.Empty);
    }
}
public sealed class ReadOwnBookingsHandler(ICurrentActor actor, IReservationBookingRepository bookings)
    : IRequestHandler<ReadOwnBookingsQuery, IReadOnlyList<BookingView>>
{
    public async Task<IReadOnlyList<BookingView>> Handle(ReadOwnBookingsQuery query, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        return (await bookings.ListOwnBookingsAsync(account, query.PageSize, query.Before, query.BeforeId, ct))
            .Select(BookingContracts.View).ToArray();
    }
}

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
        row.BookedAmount, row.DepositAmount, row.Currency, row.ConfirmedAt, row.CreatedAt);
}
