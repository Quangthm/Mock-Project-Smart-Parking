using FluentValidation;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReviewReservationQuoteValidator : AbstractValidator<ReviewReservationQuoteCommand>
{
    public ReviewReservationQuoteValidator()
    {
        RuleFor(x => x.LicensePlate).Must(raw => ReservationCarPlate.Canonicalize(raw) is not null)
            .WithMessage("A valid registered-format CAR plate is required.");
        RuleFor(x => x.RequestedSlotId).NotEmpty();
        RuleFor(x => x.Start).NotEmpty();
        RuleFor(x => x.End).GreaterThan(x => x.Start);
        RuleFor(x => x.PaymentMethod).Equal("PAYOS");
    }
}
