using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreUnit;

public sealed class RestoreUnitCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<RestoreUnitCommandHandler> logger)
    : IRequestHandler<RestoreUnitCommand>
{
    public async Task Handle(RestoreUnitCommand request, CancellationToken ct)
    {
        logger.LogInformation("Restoring unit {UnitId} in site {SiteId}", request.UnitId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.RestoreUnit(request.UnitId), ct);
    }
}
