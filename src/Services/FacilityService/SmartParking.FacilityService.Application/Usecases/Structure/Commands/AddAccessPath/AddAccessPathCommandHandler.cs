using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddAccessPath;

public sealed class AddAccessPathCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IStructureUnitOfWork unitOfWork,
    ILogger<AddAccessPathCommandHandler> logger)
    : IRequestHandler<AddAccessPathCommand, Guid>
{
    public async Task<Guid> Handle(AddAccessPathCommand request, CancellationToken ct)
    {
        logger.LogInformation("Adding access path {PathCode} to site {SiteId}", request.Code, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await unitOfWork.BeginEditAsync(owner, request.SiteId, ct);
        var id = session.Structure.AddAccessPath(request.Code, request.From, request.To, request.MapData);
        await session.CommitAsync(ct);
        return id;
    }
}
