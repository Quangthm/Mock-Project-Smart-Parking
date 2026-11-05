using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.ProcessPaymentNotification;

public class ProcessPaymentNotificationCommandValidator : AbstractValidator<ProcessPaymentNotificationCommand>
{
    public ProcessPaymentNotificationCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId is required.");

        RuleFor(x => x.PaymentOrderId)
            .NotEmpty().WithMessage("PaymentOrderId is required.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Amount must be non-negative.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("PaymentMethod is required.");
    }
}
