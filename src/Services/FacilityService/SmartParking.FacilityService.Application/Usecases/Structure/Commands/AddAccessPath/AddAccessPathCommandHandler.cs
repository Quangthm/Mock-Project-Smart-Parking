using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddAccessPath;

public sealed class AddAccessPathCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<AddAccessPathCommandHandler> logger)
    : IRequestHandler<AddAccessPathCommand, Guid>
{
    public async Task<Guid> Handle(AddAccessPathCommand request, CancellationToken ct)
    {
        logger.LogInformation("Adding access path {PathCode} to site {SiteId}", request.Code, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        return await structureService.AddAccessPathAsync(
            owner, request.SiteId, request.Code, request.From, request.To, request.MapData, ct);
    }
}
