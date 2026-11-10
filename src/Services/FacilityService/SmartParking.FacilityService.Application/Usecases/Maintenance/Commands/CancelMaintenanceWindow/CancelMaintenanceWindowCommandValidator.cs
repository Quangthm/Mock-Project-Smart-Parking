using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.CancelMaintenanceWindow;

public sealed class CancelMaintenanceWindowCommandValidator : AbstractValidator<CancelMaintenanceWindowCommand>
{
    public CancelMaintenanceWindowCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.WindowId).NotEmpty();
    }
}
