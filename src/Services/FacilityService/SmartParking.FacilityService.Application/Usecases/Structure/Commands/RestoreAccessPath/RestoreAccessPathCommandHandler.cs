using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreAccessPath;

public sealed class RestoreAccessPathCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<RestoreAccessPathCommandHandler> logger)
    : IRequestHandler<RestoreAccessPathCommand>
{
    public async Task Handle(RestoreAccessPathCommand request, CancellationToken ct)
    {
        logger.LogInformation("Restoring access path {PathId} in site {SiteId}", request.PathId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.RestorePath(request.PathId), ct);
    }
}
