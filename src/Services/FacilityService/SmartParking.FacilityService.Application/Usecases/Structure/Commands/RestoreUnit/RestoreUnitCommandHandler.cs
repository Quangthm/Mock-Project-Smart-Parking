using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreUnit;

public sealed class RestoreUnitCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IMediator mediator,
    ILogger<RestoreUnitCommandHandler> logger)
    : IRequestHandler<RestoreUnitCommand>
{
    public async Task Handle(RestoreUnitCommand request, CancellationToken ct)
    {
        logger.LogInformation("Restoring unit {UnitId} in site {SiteId}", request.UnitId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await mediator.Send(new BeginStructureEditCommand(owner, request.SiteId), ct);
        session.Structure.RestoreUnit(request.UnitId);
        await session.CommitAsync(ct);
    }
}
