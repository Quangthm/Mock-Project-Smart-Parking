using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.AddUnit;

public sealed class AddUnitCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IStructureUnitOfWork unitOfWork,
    ILogger<AddUnitCommandHandler> logger)
    : IRequestHandler<AddUnitCommand, Guid>
{
    public async Task<Guid> Handle(AddUnitCommand request, CancellationToken ct)
    {
        logger.LogInformation("Adding unit {UnitName} to site {SiteId}", request.Name, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        await using var session = await unitOfWork.BeginEditAsync(owner, request.SiteId, ct);
        var id = session.Structure.AddUnit(request.Type, request.Name, request.ParentId, request.Capacity);
        await session.CommitAsync(ct);
        return id;
    }
}
