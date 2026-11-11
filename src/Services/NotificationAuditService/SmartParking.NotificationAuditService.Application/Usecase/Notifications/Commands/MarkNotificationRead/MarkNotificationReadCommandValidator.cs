using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkNotificationRead;

public class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Notification Id is required.");
    }
}
