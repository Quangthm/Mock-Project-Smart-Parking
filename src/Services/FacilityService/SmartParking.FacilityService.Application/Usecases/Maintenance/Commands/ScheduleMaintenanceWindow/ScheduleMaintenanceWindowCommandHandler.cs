using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ScheduleMaintenanceWindow;

public sealed class ScheduleMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    MaintenanceWindowService windowService,
    ILogger<ScheduleMaintenanceWindowCommandHandler> logger)
    : IRequestHandler<ScheduleMaintenanceWindowCommand, MaintenanceWindow>
{
    public async Task<MaintenanceWindow> Handle(ScheduleMaintenanceWindowCommand request, CancellationToken ct)
    {
        logger.LogInformation("Scheduling maintenance window for site {SiteId} from {Start} to {End}",
            request.SiteId, request.Start, request.End);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        return await windowService.CreateAsync(
            owner, request.SiteId, request.Start, request.End, request.Reason, null, null, ct);
    }
}
