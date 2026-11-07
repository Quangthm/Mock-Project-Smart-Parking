using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperatorLayout;

public sealed class GetOperatorLayoutQueryValidator : AbstractValidator<GetOperatorLayoutQuery>
{
    public GetOperatorLayoutQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
    }
}
