using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ExtendMaintenanceWindow;

public sealed record ExtendMaintenanceWindowCommand(
    Guid SiteId,
    Guid WindowId,
    DateTime? Start,
    DateTime? End,
    Guid? TenantId = null) : IRequest<MaintenanceWindow>;
