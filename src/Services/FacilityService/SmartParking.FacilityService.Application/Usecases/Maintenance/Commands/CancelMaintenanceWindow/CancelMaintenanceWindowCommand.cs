using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.CancelMaintenanceWindow;

public sealed record CancelMaintenanceWindowCommand(
    Guid SiteId,
    Guid WindowId,
    Guid? TenantId = null) : IRequest;
