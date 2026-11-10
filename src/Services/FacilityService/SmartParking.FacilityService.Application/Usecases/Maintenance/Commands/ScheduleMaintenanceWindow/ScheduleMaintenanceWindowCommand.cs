using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ScheduleMaintenanceWindow;

public sealed record ScheduleMaintenanceWindowCommand(
    Guid SiteId,
    DateTime Start,
    DateTime End,
    string Reason,
    Guid? TenantId = null) : IRequest<MaintenanceWindow>;
