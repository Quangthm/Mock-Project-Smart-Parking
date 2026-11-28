using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.SimulateDemoPayment;

public sealed class SimulateDemoPayOsPaymentCommandValidator : AbstractValidator<SimulateDemoPayOsPaymentCommand>
{
    public SimulateDemoPayOsPaymentCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required.");
    }
}
