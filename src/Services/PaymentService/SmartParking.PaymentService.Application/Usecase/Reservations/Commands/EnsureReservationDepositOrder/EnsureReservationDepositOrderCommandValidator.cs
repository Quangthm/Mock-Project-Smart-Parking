using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;

public sealed class EnsureReservationDepositOrderCommandValidator : AbstractValidator<EnsureReservationDepositOrderCommand>
{
    public EnsureReservationDepositOrderCommandValidator()
    {
        RuleFor(x => x.Request.OperationId)
            .NotEmpty()
            .WithMessage("OperationId is required.");

        RuleFor(x => x.Request.ReservationId)
            .NotEmpty()
            .WithMessage("ReservationId is required.");

        RuleFor(x => x.Request.AccountId)
            .NotEmpty()
            .WithMessage("AccountId is required.");

        RuleFor(x => x.Request.SiteId)
            .NotEmpty()
            .WithMessage("SiteId is required.");

        RuleFor(x => x.Request.QuoteId)
            .NotEmpty()
            .WithMessage("QuoteId is required.");

        RuleFor(x => x.Request.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Amount must be greater than or equal to 0.");

        RuleFor(x => x.Request.Currency)
            .Equal("VND")
            .WithMessage("Currency must be VND.");

        RuleFor(x => x.Request.PaymentMethod)
            .NotEmpty()
            .WithMessage("PaymentMethod is required.")
            .MaximumLength(32)
            .WithMessage("PaymentMethod cannot exceed 32 characters.");
    }
}
