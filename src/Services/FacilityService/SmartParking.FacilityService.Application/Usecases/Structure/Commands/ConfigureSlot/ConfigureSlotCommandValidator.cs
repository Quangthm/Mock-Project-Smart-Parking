using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureSlot;

public sealed class ConfigureSlotCommandValidator : AbstractValidator<ConfigureSlotCommand>
{
    public ConfigureSlotCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.SlotId).NotEmpty().WithMessage("SlotId is required.");
    }
}
