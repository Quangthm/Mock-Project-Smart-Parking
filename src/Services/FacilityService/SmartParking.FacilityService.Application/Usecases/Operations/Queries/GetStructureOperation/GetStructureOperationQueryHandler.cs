using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Operations.Queries.GetStructureOperation;

public sealed class GetStructureOperationQueryHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IStructureOperationStore store,
    ILogger<GetStructureOperationQueryHandler> logger)
    : IRequestHandler<GetStructureOperationQuery, StructureOperation>
{
    public async Task<StructureOperation> Handle(GetStructureOperationQuery request, CancellationToken ct)
    {
        logger.LogInformation("Reading structure operation {OperationId} for site {SiteId}",
            request.OperationId, request.SiteId);

        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current Owner access is required.");

        return await store.ReadAsync(owner, request.SiteId, request.OperationId, ct);
    }
}
