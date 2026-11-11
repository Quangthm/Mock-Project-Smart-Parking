using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetNotificationById;

public class GetNotificationByIdQueryValidator : AbstractValidator<GetNotificationByIdQuery>
{
    public GetNotificationByIdQueryValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty().WithMessage("NotificationId is required.");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty().WithMessage("RequestingUserId is required.");
    }
}
