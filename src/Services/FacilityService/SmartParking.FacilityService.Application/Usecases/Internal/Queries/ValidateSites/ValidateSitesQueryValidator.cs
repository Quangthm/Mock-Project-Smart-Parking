using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Queries.ValidateSites;

public sealed class ValidateSitesQueryValidator : AbstractValidator<ValidateSitesQuery>
{
    public ValidateSitesQueryValidator()
    {
        RuleFor(x => x.Body).NotNull();
    }
}
