using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.CancelMaintenanceWindow;

public sealed class CancelMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    MaintenanceWindowService windowService,
    ILogger<CancelMaintenanceWindowCommandHandler> logger)
    : IRequestHandler<CancelMaintenanceWindowCommand>
{
    public async Task Handle(CancelMaintenanceWindowCommand request, CancellationToken ct)
    {
        logger.LogInformation("Cancelling maintenance window {WindowId} for site {SiteId}",
            request.WindowId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await windowService.CancelAsync(owner, request.SiteId, request.WindowId, ct);
    }
}
