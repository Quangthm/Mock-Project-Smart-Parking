using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetDriverNotifications;

public class GetDriverNotificationsQueryValidator : AbstractValidator<GetDriverNotificationsQuery>
{
    public GetDriverNotificationsQueryValidator()
    {
        RuleFor(x => x.RecipientId)
            .NotEmpty().WithMessage("RecipientId is required.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100).WithMessage("Limit must be between 1 and 100.");
    }
}
