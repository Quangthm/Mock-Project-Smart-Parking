using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddUnit;

public sealed class AddUnitCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IMediator mediator,
    ILogger<AddUnitCommandHandler> logger)
    : IRequestHandler<AddUnitCommand, Guid>
{
    public async Task<Guid> Handle(AddUnitCommand request, CancellationToken ct)
    {
        logger.LogInformation("Adding unit {UnitName} to site {SiteId}", request.Name, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await mediator.Send(new BeginStructureEditCommand(owner, request.SiteId), ct);
        var id = session.Structure.AddUnit(request.Type, request.Name, request.ParentId, request.Capacity);
        await session.CommitAsync(ct);
        return id;
    }
}
