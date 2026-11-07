using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreSlot;

public sealed class RestoreSlotCommandValidator : AbstractValidator<RestoreSlotCommand>
{
    public RestoreSlotCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.SlotId).NotEmpty().WithMessage("SlotId is required.");
    }
}
