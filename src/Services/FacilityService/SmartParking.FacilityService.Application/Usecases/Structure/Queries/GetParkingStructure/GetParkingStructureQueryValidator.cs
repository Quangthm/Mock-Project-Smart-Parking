using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Queries.GetParkingStructure;

public sealed class GetParkingStructureQueryValidator : AbstractValidator<GetParkingStructureQuery>
{
    public GetParkingStructureQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
    }
}
