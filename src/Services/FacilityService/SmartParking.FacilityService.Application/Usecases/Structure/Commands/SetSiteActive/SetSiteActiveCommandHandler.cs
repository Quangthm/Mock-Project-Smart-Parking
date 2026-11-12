using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.SetSiteActive;

public sealed class SetSiteActiveCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IMediator mediator,
    ILogger<SetSiteActiveCommandHandler> logger)
    : IRequestHandler<SetSiteActiveCommand>
{
    public async Task Handle(SetSiteActiveCommand request, CancellationToken ct)
    {
        logger.LogInformation("Setting site active status to {Active} for SiteId {SiteId}", request.Active, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await mediator.Send(new BeginStructureEditCommand(owner, request.SiteId), ct);
        session.Structure.SetActive(request.Active);
        await session.CommitAsync(ct);
    }
}
