using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveAccessPath;

public sealed class RemoveAccessPathCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<RemoveAccessPathCommandHandler> logger)
    : IRequestHandler<RemoveAccessPathCommand>
{
    public async Task Handle(RemoveAccessPathCommand request, CancellationToken ct)
    {
        logger.LogInformation("Removing access path {PathId} from site {SiteId}", request.PathId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.RemoveAccessPath(request.PathId), ct);
    }
}
