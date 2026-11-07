using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.CollectCashPayment;

public sealed class CollectCashPaymentCommandValidator : AbstractValidator<CollectCashPaymentCommand>
{
    public CollectCashPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("OrderId is required.");

        RuleFor(x => x.AmountReceived)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AmountReceived.HasValue)
            .WithMessage("AmountReceived must be greater than or equal to 0.");
    }
}
