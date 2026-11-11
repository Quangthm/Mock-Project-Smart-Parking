using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.RetryNotificationDelivery;

public class RetryNotificationDeliveryCommandValidator : AbstractValidator<RetryNotificationDeliveryCommand>
{
    public RetryNotificationDeliveryCommandValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty().WithMessage("NotificationId is required.");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty().WithMessage("RequestingUserId is required.");
    }
}
