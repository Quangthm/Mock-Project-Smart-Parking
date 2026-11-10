using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetSiteActive;

public sealed class SetSiteActiveCommandValidator : AbstractValidator<SetSiteActiveCommand>
{
    public SetSiteActiveCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
    }
}
