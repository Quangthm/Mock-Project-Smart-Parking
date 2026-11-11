using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveSlot;

public sealed class RemoveSlotCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IMediator mediator,
    ILogger<RemoveSlotCommandHandler> logger)
    : IRequestHandler<RemoveSlotCommand>
{
    public async Task Handle(RemoveSlotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Removing slot {SlotId} from site {SiteId}", request.SlotId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await mediator.Send(new BeginStructureEditCommand(owner, request.SiteId), ct);
        session.Structure.RemoveSlot(request.SlotId);
        await session.CommitAsync(ct);
    }
}
