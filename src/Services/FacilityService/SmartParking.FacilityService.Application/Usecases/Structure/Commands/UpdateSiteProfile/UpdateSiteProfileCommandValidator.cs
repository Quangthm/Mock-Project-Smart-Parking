using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.UpdateSiteProfile;

public sealed class UpdateSiteProfileCommandValidator : AbstractValidator<UpdateSiteProfileCommand>
{
    public UpdateSiteProfileCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).WithMessage("Site code must be 1-50 characters.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).WithMessage("Site name must be 1-255 characters.");
        RuleFor(x => x.Address).NotEmpty().MaximumLength(2000).WithMessage("Site address must be 1-2000 characters.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).When(x => x.Longitude.HasValue);
    }
}
