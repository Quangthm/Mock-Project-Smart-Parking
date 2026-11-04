using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveUnit;

public sealed class RemoveUnitCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<RemoveUnitCommandHandler> logger)
    : IRequestHandler<RemoveUnitCommand>
{
    public async Task Handle(RemoveUnitCommand request, CancellationToken ct)
    {
        logger.LogInformation("Removing unit {UnitId} from site {SiteId}", request.UnitId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.RemoveUnit(request.UnitId), ct);
    }
}
