using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RenameUnit;

public sealed class RenameUnitCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IMediator mediator,
    ILogger<RenameUnitCommandHandler> logger)
    : IRequestHandler<RenameUnitCommand>
{
    public async Task Handle(RenameUnitCommand request, CancellationToken ct)
    {
        logger.LogInformation("Renaming unit {UnitId} to {Name} in site {SiteId}", request.UnitId, request.Name, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await mediator.Send(new BeginStructureEditCommand(owner, request.SiteId), ct);
        session.Structure.RenameUnit(request.UnitId, request.Name);
        await session.CommitAsync(ct);
    }
}
