using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreUnit;

public sealed class RestoreUnitCommandValidator : AbstractValidator<RestoreUnitCommand>
{
    public RestoreUnitCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.UnitId).NotEmpty().WithMessage("UnitId is required.");
    }
}
