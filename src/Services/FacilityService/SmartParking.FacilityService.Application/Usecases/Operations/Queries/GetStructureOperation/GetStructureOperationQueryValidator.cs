using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Operations.Queries.GetStructureOperation;

public sealed class GetStructureOperationQueryValidator : AbstractValidator<GetStructureOperationQuery>
{
    public GetStructureOperationQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.OperationId).NotEmpty();
    }
}
