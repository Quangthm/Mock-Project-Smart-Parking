using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreAccessPath;

public sealed class RestoreAccessPathCommandValidator : AbstractValidator<RestoreAccessPathCommand>
{
    public RestoreAccessPathCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.PathId).NotEmpty().WithMessage("PathId is required.");
    }
}
