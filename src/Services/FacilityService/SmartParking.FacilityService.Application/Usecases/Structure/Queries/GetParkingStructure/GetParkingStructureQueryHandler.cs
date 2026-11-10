using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Queries.GetParkingStructure;

public sealed class GetParkingStructureQueryHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IParkingStructureRepository repository,
    IMaintenanceWindowStore windows,
    ILogger<GetParkingStructureQueryHandler> logger)
    : IRequestHandler<GetParkingStructureQuery, ParkingStructureResponse>
{
    public async Task<ParkingStructureResponse> Handle(GetParkingStructureQuery request, CancellationToken ct)
    {
        logger.LogInformation("Retrieving parking structure for SiteId {SiteId}", request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");

        // Pure read-only snapshot: lock-free, no advisory locks, no synchronization rows, no inter-service write holds
        var structure = await repository.ReadAsync(owner, request.SiteId, null, ct);
        var windowList = await windows.ListAsync(owner, request.SiteId, ct);
        var now = DateTime.UtcNow;
        var state = MaintenanceWindowState.Derive(windowList, structure.Site.Status, now);
        var closedUntil = MaintenanceWindowState.ClosedUntil(windowList, now);

        return new ParkingStructureResponse(
            structure.Site,
            structure.Units,
            structure.Slots,
            structure.Paths,
            structure.BackupPolicies,
            structure.CapacityViews,
            new ServiceStateResponse(
                state,
                closedUntil,
                windowList.Select(w => new MaintenanceWindowResponseItem(
                    w.Id,
                    w.Start,
                    w.End,
                    w.Reason,
                    w.EnforcementState
                ))
            )
        );
    }
}
