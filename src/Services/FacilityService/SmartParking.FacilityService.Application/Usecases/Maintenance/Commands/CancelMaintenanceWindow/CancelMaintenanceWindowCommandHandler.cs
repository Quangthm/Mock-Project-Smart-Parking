using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.CancelMaintenanceWindow;

public sealed class CancelMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IMaintenanceWindowStore store,
    ILogger<CancelMaintenanceWindowCommandHandler> logger)
    : IRequestHandler<CancelMaintenanceWindowCommand>
{
    public async Task Handle(CancelMaintenanceWindowCommand request, CancellationToken ct)
    {
        logger.LogInformation("Cancelling maintenance window {WindowId} for site {SiteId}",
            request.WindowId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");

        await store.CancelAsync(owner, request.SiteId, request.WindowId, ct);
    }
}
