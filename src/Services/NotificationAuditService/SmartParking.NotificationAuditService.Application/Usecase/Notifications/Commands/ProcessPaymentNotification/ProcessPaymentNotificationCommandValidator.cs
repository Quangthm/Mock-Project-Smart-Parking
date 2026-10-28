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

        RuleFor(x => x.DriverId)
            .NotEmpty().WithMessage("DriverId is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");
    }
}
