using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ExtendMaintenanceWindow;

public sealed class ExtendMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    MaintenanceWindowService windowService,
    ILogger<ExtendMaintenanceWindowCommandHandler> logger)
    : IRequestHandler<ExtendMaintenanceWindowCommand, MaintenanceWindow>
{
    public async Task<MaintenanceWindow> Handle(ExtendMaintenanceWindowCommand request, CancellationToken ct)
    {
        logger.LogInformation("Extending maintenance window {WindowId} for site {SiteId}",
            request.WindowId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        return await windowService.ExtendAsync(
            owner, request.SiteId, request.WindowId, request.Start, request.End, ct);
    }
}
