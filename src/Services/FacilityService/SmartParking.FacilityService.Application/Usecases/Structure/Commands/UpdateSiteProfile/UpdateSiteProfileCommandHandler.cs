using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.UpdateSiteProfile;

public sealed class UpdateSiteProfileCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<UpdateSiteProfileCommandHandler> logger)
    : IRequestHandler<UpdateSiteProfileCommand>
{
    public async Task Handle(UpdateSiteProfileCommand request, CancellationToken ct)
    {
        logger.LogInformation("Updating site profile for SiteId {SiteId}", request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.UpdateProfile(request.Code, request.Name, request.Address, request.Latitude, request.Longitude), ct);
    }
}
