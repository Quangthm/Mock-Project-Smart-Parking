using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveUnit;

public sealed class RemoveUnitCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IMediator mediator,
    ILogger<RemoveUnitCommandHandler> logger)
    : IRequestHandler<RemoveUnitCommand>
{
    public async Task Handle(RemoveUnitCommand request, CancellationToken ct)
    {
        logger.LogInformation("Removing unit {UnitId} from site {SiteId}", request.UnitId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await mediator.Send(new BeginStructureEditCommand(owner, request.SiteId), ct);
        session.Structure.RemoveUnit(request.UnitId);
        await session.CommitAsync(ct);
    }
}
