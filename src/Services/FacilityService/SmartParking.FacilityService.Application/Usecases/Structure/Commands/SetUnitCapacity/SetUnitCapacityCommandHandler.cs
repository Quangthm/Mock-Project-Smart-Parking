using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetUnitCapacity;

public sealed class SetUnitCapacityCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<SetUnitCapacityCommandHandler> logger)
    : IRequestHandler<SetUnitCapacityCommand>
{
    public async Task Handle(SetUnitCapacityCommand request, CancellationToken ct)
    {
        logger.LogInformation("Setting capacity of unit {UnitId} to {Capacity} in site {SiteId}",
            request.UnitId, request.Capacity, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.SetUnitCapacity(request.UnitId, request.Capacity), ct);
    }
}
