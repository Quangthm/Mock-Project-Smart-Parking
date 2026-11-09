using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.ForceReleaseStructureHolds;

public sealed class ForceReleaseStructureHoldsCommandValidator : AbstractValidator<ForceReleaseStructureHoldsCommand>
{
    public ForceReleaseStructureHoldsCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required.");
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
    }
}
