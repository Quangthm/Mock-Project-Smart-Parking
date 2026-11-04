using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Operations.Queries.GetStructureOperation;

public sealed class GetStructureOperationQueryHandler(
    IOwnerScopeResolver scopeResolver,
    StructureOperationService operations,
    ILogger<GetStructureOperationQueryHandler> logger)
    : IRequestHandler<GetStructureOperationQuery, StructureOperation>
{
    public async Task<StructureOperation> Handle(GetStructureOperationQuery request, CancellationToken ct)
    {
        logger.LogInformation("Reading structure operation {OperationId} for site {SiteId}",
            request.OperationId, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        return await operations.ReadAsync(owner, request.SiteId, request.OperationId, ct);
    }
}
