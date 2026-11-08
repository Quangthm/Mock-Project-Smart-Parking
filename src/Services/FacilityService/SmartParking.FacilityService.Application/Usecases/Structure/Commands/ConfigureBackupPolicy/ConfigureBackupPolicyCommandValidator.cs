using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureBackupPolicy;

public sealed class ConfigureBackupPolicyCommandValidator : AbstractValidator<ConfigureBackupPolicyCommand>
{
    public ConfigureBackupPolicyCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId is required.");
        RuleFor(x => x.VehicleType).NotNull().WithMessage("Vehicle type is required.");
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).WithMessage("Capacity must be non-negative.");
    }
}
