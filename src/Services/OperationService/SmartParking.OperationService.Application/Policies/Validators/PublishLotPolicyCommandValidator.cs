using FluentValidation;
using SmartParking.OperationService.Application.Policies.Commands;

namespace SmartParking.OperationService.Application.Policies.Validators;

public sealed class PublishLotPolicyCommandValidator : AbstractValidator<PublishLotPolicyCommand>
{
    public PublishLotPolicyCommandValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithMessage("siteId is required.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithErrorCode("INVALID_REQUEST")
            .WithMessage("Idempotency-Key header is required for publishing policy.");

        RuleFor(x => x.Request.Mode)
            .Must(m => m.Equals("IMMEDIATE", StringComparison.OrdinalIgnoreCase) ||
                       m.Equals("SCHEDULED", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("INVALID_REQUEST")
            .WithMessage("mode must be either 'IMMEDIATE' or 'SCHEDULED'.");

        RuleFor(x => x.Request.ScheduledAt)
            .NotNull()
            .Must(at => at > DateTimeOffset.UtcNow)
            .When(x => x.Request.Mode.Equals("SCHEDULED", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("INVALID_ACTIVATION_SCHEDULE")
            .WithMessage("scheduledAt is required and must be a future timestamp when mode is SCHEDULED.");
    }
}
