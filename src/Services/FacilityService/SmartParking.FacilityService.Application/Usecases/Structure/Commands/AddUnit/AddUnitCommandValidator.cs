using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddUnit;

public sealed class AddUnitCommandValidator : AbstractValidator<AddUnitCommand>
{
    public AddUnitCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Unit name must be 1-100 characters.");
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).WithMessage("Capacity must be non-negative.");
    }
}
