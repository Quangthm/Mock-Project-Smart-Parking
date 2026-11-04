using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseGateSlot;

public sealed class ReleaseGateSlotCommandValidator : AbstractValidator<ReleaseGateSlotCommand>
{
    public ReleaseGateSlotCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SlotId).NotEmpty();
    }
}
