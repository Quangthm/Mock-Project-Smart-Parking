using FluentValidation;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class AdmitQuoteReservationValidator : AbstractValidator<AdmitQuoteReservation>
{
    public AdmitQuoteReservationValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Intent).NotNull();
        When(x => x.Intent is not null, () =>
        {
            RuleFor(x => x.Intent.LicensePlate).Must(x => ReservationCarPlate.Canonicalize(x) is not null);
            RuleFor(x => x.Intent.RequestedSlotId).NotEmpty();
            RuleFor(x => x.Intent.Start).NotEmpty();
            RuleFor(x => x.Intent.End).GreaterThan(x => x.Intent.Start);
            RuleFor(x => x.Intent.PaymentMethod).Equal("PAYOS");
            RuleFor(x => x.Intent.QuoteId).NotEmpty();
        });
    }
}
