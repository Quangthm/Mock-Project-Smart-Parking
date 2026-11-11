using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

public class ProcessPayOsWebhookCommandValidator : AbstractValidator<ProcessPayOsWebhookCommand>
{
    public ProcessPayOsWebhookCommandValidator()
    {
        RuleFor(x => x.RawPayload)
            .NotEmpty().WithMessage("The webhook payload is required.");

        RuleFor(x => x.Signature)
            .NotEmpty().WithMessage("The webhook signature is required.");
    }
}
