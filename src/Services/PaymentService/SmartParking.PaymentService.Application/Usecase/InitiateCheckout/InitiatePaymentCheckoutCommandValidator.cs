using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.InitiateCheckout;

public class InitiatePaymentCheckoutCommandValidator : AbstractValidator<InitiatePaymentCheckoutCommand>
{
    public InitiatePaymentCheckoutCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty().WithMessage("Mã đơn hàng (orderId) là bắt buộc.");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum().WithMessage("Phương thức thanh toán không hợp lệ.");

        When(x => !string.IsNullOrEmpty(x.ReturnUrl), () =>
        {
            RuleFor(x => x.ReturnUrl)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("ReturnUrl phải là một URL hợp lệ.");
        });

        When(x => !string.IsNullOrEmpty(x.CancelUrl), () =>
        {
            RuleFor(x => x.CancelUrl)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("CancelUrl phải là một URL hợp lệ.");
        });
    }
}
