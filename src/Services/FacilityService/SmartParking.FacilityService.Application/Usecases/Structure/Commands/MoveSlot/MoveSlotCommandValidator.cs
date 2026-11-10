using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.MoveSlot;

public sealed class MoveSlotCommandValidator : AbstractValidator<MoveSlotCommand>
{
    public MoveSlotCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.SlotId).NotEmpty().WithMessage("SlotId is required.");
        RuleFor(x => x.TargetUnitId).NotEmpty().WithMessage("TargetUnitId is required.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).WithMessage("Slot code must be 1-50 characters.");
    }
}
