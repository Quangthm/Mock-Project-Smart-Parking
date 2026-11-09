using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveUnit;

public sealed class RemoveUnitCommandValidator : AbstractValidator<RemoveUnitCommand>
{
    public RemoveUnitCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.UnitId).NotEmpty().WithMessage("UnitId is required.");
    }
}
