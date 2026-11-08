using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveSlot;

public sealed class RemoveSlotCommandValidator : AbstractValidator<RemoveSlotCommand>
{
    public RemoveSlotCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.SlotId).NotEmpty().WithMessage("SlotId is required.");
    }
}
