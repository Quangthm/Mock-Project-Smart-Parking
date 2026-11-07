using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ClaimGateSlot;

public sealed class ClaimGateSlotCommandValidator : AbstractValidator<ClaimGateSlotCommand>
{
    public ClaimGateSlotCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.VehicleType).Must(v => v is "CAR" or "MOTORCYCLE" or "ANY")
            .WithMessage("vehicleType must be CAR, MOTORCYCLE, or ANY.");
    }
}
