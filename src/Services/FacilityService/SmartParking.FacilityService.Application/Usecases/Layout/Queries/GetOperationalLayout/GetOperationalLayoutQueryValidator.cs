using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperationalLayout;

public sealed class GetOperationalLayoutQueryValidator : AbstractValidator<GetOperationalLayoutQuery>
{
    public GetOperationalLayoutQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
    }
}
