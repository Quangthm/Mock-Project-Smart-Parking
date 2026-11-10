using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ScheduleMaintenanceWindow;

public sealed class ScheduleMaintenanceWindowCommandValidator : AbstractValidator<ScheduleMaintenanceWindowCommand>
{
    public ScheduleMaintenanceWindowCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}
