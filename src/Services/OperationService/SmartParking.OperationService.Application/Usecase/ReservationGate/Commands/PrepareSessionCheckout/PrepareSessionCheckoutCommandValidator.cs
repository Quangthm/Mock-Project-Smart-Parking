using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class PrepareSessionCheckoutCommandValidator : AbstractValidator<PrepareSessionCheckoutCommand>
{
    public PrepareSessionCheckoutCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("SessionId is required.");
    }
}