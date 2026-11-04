using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RenameUnit;

public sealed class RenameUnitCommandValidator : AbstractValidator<RenameUnitCommand>
{
    public RenameUnitCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.UnitId).NotEmpty().WithMessage("UnitId is required.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Unit name must be 1-100 characters.");
    }
}
