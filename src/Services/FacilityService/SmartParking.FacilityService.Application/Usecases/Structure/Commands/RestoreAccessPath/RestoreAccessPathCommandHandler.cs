using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.RestoreAccessPath;

public sealed class RestoreAccessPathCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IStructureUnitOfWork unitOfWork,
    ILogger<RestoreAccessPathCommandHandler> logger)
    : IRequestHandler<RestoreAccessPathCommand>
{
    public async Task Handle(RestoreAccessPathCommand request, CancellationToken ct)
    {
        logger.LogInformation("Restoring access path {PathId} in site {SiteId}", request.PathId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await unitOfWork.BeginEditAsync(owner, request.SiteId, ct);
        session.Structure.RestorePath(request.PathId);
        await session.CommitAsync(ct);
    }
}
