using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddUnit;

public sealed class AddUnitCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<AddUnitCommandHandler> logger)
    : IRequestHandler<AddUnitCommand, Guid>
{
    public async Task<Guid> Handle(AddUnitCommand request, CancellationToken ct)
    {
        logger.LogInformation("Adding unit {UnitName} to site {SiteId}", request.Name, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        return await structureService.AddUnitAsync(
            owner, request.SiteId, request.Type, request.Name, request.ParentId, request.Capacity, ct);
    }
}
