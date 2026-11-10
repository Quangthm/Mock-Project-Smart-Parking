using FluentValidation;

namespace SmartParking.OperationService.Application.Usecase.Structure.Queries.ReadStructureSnapshot;

public sealed class ReadStructureSnapshotQueryValidator : AbstractValidator<ReadStructureSnapshotQuery>
{
    public ReadStructureSnapshotQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required.");
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
    }
}
