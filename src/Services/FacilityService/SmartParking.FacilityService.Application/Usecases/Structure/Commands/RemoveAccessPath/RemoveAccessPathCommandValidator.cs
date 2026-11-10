using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveAccessPath;

public sealed class RemoveAccessPathCommandValidator : AbstractValidator<RemoveAccessPathCommand>
{
    public RemoveAccessPathCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.PathId).NotEmpty().WithMessage("PathId is required.");
    }
}
