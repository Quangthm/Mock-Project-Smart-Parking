using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.CancelMaintenanceWindow;

public sealed class CancelMaintenanceWindowCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IMaintenanceWindowStore store,
    ILogger<CancelMaintenanceWindowCommandHandler> logger,
    IMaintenanceEnforcementClient? enforcement = null)
    : IRequestHandler<CancelMaintenanceWindowCommand>
{
    public async Task Handle(CancelMaintenanceWindowCommand request, CancellationToken ct)
    {
        logger.LogInformation("Cancelling maintenance window {WindowId} for site {SiteId}",
            request.WindowId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");

        // Local cancellation commits first; the remote release is a separate step so a transport
        // failure leaves a cancelled row that MaintenanceEnforcementWorker can retry.
        await store.CancelAsync(owner, request.SiteId, request.WindowId, ct);
        if (enforcement is not null &&
            await enforcement.CancelAsync(owner.TenantId, request.SiteId, request.WindowId, ct))
            await store.ClearEnforcementRefAsync(owner.TenantId, request.SiteId, request.WindowId, ct);
    }
}
