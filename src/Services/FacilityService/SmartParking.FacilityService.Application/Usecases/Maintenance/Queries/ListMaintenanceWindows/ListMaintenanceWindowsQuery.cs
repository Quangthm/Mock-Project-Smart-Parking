using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Queries.ListMaintenanceWindows;

public sealed record ListMaintenanceWindowsQuery(
    Guid SiteId,
    Guid? TenantId = null) : IRequest<IReadOnlyList<MaintenanceWindow>>;
