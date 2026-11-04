using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Usecases.Operations.Commands.ExecuteStructureOperation;

public sealed class ExecuteStructureOperationCommandHandler(
    IOwnerScopeResolver scopeResolver,
    StructureOperationService operations,
    ILogger<ExecuteStructureOperationCommandHandler> logger)
    : IRequestHandler<ExecuteStructureOperationCommand, StructureOperation>
{
    public async Task<StructureOperation> Handle(ExecuteStructureOperationCommand request, CancellationToken ct)
    {
        logger.LogInformation("Executing structure operation {OperationKey} for site {SiteId}",
            request.IdempotencyKey, request.SiteId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        return await operations.ExecuteAsync(owner, request.SiteId, request.IdempotencyKey, request.Edit, ct);
    }
}
