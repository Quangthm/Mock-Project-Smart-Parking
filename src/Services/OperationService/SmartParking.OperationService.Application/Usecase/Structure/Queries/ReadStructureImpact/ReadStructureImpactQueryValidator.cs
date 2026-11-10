using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.Structure.Queries.ReadStructureImpact;

public sealed class ReadStructureImpactQueryValidator : AbstractValidator<ReadStructureImpactQuery>
{
    public ReadStructureImpactQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required.");
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
    }
}
