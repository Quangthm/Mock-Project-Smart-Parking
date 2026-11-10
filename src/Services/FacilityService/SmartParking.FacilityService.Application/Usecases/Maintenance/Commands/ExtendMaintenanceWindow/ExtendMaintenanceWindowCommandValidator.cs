using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ExtendMaintenanceWindow;

public sealed class ExtendMaintenanceWindowCommandValidator : AbstractValidator<ExtendMaintenanceWindowCommand>
{
    public ExtendMaintenanceWindowCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.WindowId).NotEmpty();
    }
}
