using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Maintenance.Queries.ListMaintenanceWindows;

public sealed class ListMaintenanceWindowsQueryHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IMaintenanceWindowStore store,
    ILogger<ListMaintenanceWindowsQueryHandler> logger)
    : IRequestHandler<ListMaintenanceWindowsQuery, IReadOnlyList<MaintenanceWindow>>
{
    public async Task<IReadOnlyList<MaintenanceWindow>> Handle(ListMaintenanceWindowsQuery request, CancellationToken ct)
    {
        logger.LogInformation("Listing maintenance windows for site {SiteId}", request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");

        return await store.ListAsync(owner, request.SiteId, ct);
    }
}
