using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections;

public class RecordAuditProjectionCommandValidator : AbstractValidator<RecordAuditProjectionCommand>
{
    public RecordAuditProjectionCommandValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("EventId is required.");

        RuleFor(x => x.OriginatingService)
            .NotEmpty().WithMessage("OriginatingService is required.")
            .MaximumLength(100).WithMessage("OriginatingService must not exceed 100 characters.");

        RuleFor(x => x.ActorId)
            .NotEmpty().WithMessage("ActorId is required.");

        RuleFor(x => x.ActorRole)
            .NotEmpty().WithMessage("ActorRole is required.")
            .MaximumLength(50).WithMessage("ActorRole must not exceed 50 characters.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required.")
            .MaximumLength(100).WithMessage("Action must not exceed 100 characters.");

        RuleFor(x => x.ResourceType)
            .NotEmpty().WithMessage("ResourceType is required.")
            .MaximumLength(100).WithMessage("ResourceType must not exceed 100 characters.");

        RuleFor(x => x.ResourceId)
            .NotEmpty().WithMessage("ResourceId is required.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("Details is required.");

        RuleFor(x => x.OccurredAt)
            .NotEmpty().WithMessage("OccurredAt is required.");
    }
}
