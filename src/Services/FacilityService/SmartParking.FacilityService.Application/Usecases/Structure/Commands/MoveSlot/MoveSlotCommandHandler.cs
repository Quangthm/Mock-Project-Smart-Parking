using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.MoveSlot;

public sealed class MoveSlotCommandHandler(
    IOwnerScopeResolver scopeResolver,
    ParkingStructureService structureService,
    ILogger<MoveSlotCommandHandler> logger)
    : IRequestHandler<MoveSlotCommand>
{
    public async Task Handle(MoveSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Moving slot {SlotId} to unit {TargetUnitId} with code {Code} in site {SiteId}",
            request.SlotId, request.TargetUnitId, request.Code, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await structureService.ChangeAsync(owner, request.SiteId,
            structure => structure.MoveSlot(request.SlotId, request.TargetUnitId, request.Code), ct);
    }
}
