using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetSiteActive;

public sealed class SetSiteActiveCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<SetSiteActiveCommandHandler> logger)
    : IRequestHandler<SetSiteActiveCommand>
{
    public async Task Handle(SetSiteActiveCommand request, CancellationToken ct)
    {
        logger.LogInformation("Setting site active status to {Active} for SiteId {SiteId}", request.Active, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.SetActive(request.Active), ct);
    }
}
