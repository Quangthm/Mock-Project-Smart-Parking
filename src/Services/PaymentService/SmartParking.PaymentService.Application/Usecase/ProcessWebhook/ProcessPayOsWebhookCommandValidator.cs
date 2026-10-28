using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

public class ProcessPayOsWebhookCommandValidator : AbstractValidator<ProcessPayOsWebhookCommand>
{
    public ProcessPayOsWebhookCommandValidator()
    {
        RuleFor(x => x.RawPayload)
            .NotEmpty().WithMessage("Payload webhook không được để trống.");

        RuleFor(x => x.Signature)
            .NotEmpty().WithMessage("Chữ ký webhook (signature) là bắt buộc.");
    }
}
