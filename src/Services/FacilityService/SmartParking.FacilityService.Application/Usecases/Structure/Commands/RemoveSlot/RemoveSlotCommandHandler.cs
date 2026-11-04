using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveSlot;

public sealed class RemoveSlotCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<RemoveSlotCommandHandler> logger)
    : IRequestHandler<RemoveSlotCommand>
{
    public async Task Handle(RemoveSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Removing slot {SlotId} from site {SiteId}", request.SlotId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.RemoveSlot(request.SlotId), ct);
    }
}
