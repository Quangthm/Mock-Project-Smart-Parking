using FluentValidation;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed class DependencyIntentValidator : AbstractValidator<DependencyIntent>
{
    public DependencyIntentValidator()
    {
        RuleFor(x => x.Token).NotEmpty(); RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty(); RuleFor(x => x.SiteId).NotEmpty(); RuleFor(x => x.SlotId).NotEmpty();
        RuleFor(x => x.Purpose).Must(x => x == "CONFIRMATION");
        RuleFor(x => x.IntentFingerprint).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Deadline).NotEmpty();
    }
}
