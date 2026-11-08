using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class PrepareReservationCheckoutCommandValidator : AbstractValidator<PrepareReservationCheckoutCommand>
{
    public PrepareReservationCheckoutCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.ReservationId).NotEmpty().WithMessage("ReservationId is required.");
    }
}