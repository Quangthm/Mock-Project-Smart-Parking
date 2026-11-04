using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreSlot;

public sealed class RestoreSlotCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<RestoreSlotCommandHandler> logger)
    : IRequestHandler<RestoreSlotCommand>
{
    public async Task Handle(RestoreSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Restoring slot {SlotId} in site {SiteId}", request.SlotId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.RestoreSlot(request.SlotId), ct);
    }
}
