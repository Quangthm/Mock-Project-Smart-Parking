using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddAccessPath;

public sealed class AddAccessPathCommandValidator : AbstractValidator<AddAccessPathCommand>
{
    public AddAccessPathCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).WithMessage("Path code must be 1-50 characters.");
    }
}
