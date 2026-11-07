using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Queries.ListMaintenanceWindows;

public sealed class ListMaintenanceWindowsQueryValidator : AbstractValidator<ListMaintenanceWindowsQuery>
{
    public ListMaintenanceWindowsQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
    }
}
