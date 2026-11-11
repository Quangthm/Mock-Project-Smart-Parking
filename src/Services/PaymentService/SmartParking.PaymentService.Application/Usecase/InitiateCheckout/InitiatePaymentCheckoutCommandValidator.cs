using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.InitiateCheckout;

public class InitiatePaymentCheckoutCommandValidator : AbstractValidator<InitiatePaymentCheckoutCommand>
{
    public InitiatePaymentCheckoutCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("The orderId is required.");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum().WithMessage("The payment method is invalid.");

        When(x => !string.IsNullOrEmpty(x.ReturnUrl), () =>
        {
            RuleFor(x => x.ReturnUrl)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("ReturnUrl must be a valid URL.");
        });

        When(x => !string.IsNullOrEmpty(x.CancelUrl), () =>
        {
            RuleFor(x => x.CancelUrl)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("CancelUrl must be a valid URL.");
        });
    }
}
