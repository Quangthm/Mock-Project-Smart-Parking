using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetUnitCapacity;

public sealed class SetUnitCapacityCommandValidator : AbstractValidator<SetUnitCapacityCommand>
{
    public SetUnitCapacityCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.UnitId).NotEmpty().WithMessage("UnitId is required.");
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).WithMessage("Capacity must be non-negative.");
    }
}
