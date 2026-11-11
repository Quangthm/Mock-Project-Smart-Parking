using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RemoveAccessPath;

public sealed class RemoveAccessPathCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IMediator mediator,
    ILogger<RemoveAccessPathCommandHandler> logger)
    : IRequestHandler<RemoveAccessPathCommand>
{
    public async Task Handle(RemoveAccessPathCommand request, CancellationToken ct)
    {
        logger.LogInformation("Removing access path {PathId} from site {SiteId}", request.PathId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await mediator.Send(new BeginStructureEditCommand(owner, request.SiteId), ct);
        session.Structure.RemoveAccessPath(request.PathId);
        await session.CommitAsync(ct);
    }
}
