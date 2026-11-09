using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddSlot;

public sealed class AddSlotCommandValidator : AbstractValidator<AddSlotCommand>
{
    public AddSlotCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.UnitId).NotEmpty().WithMessage("UnitId is required.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).WithMessage("Slot code must be 1-50 characters.");
    }
}
