using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Application.Usecases.Operations.Commands.ExecuteStructureOperation;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.BatchEditStructure;

public sealed class BatchEditStructureCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IMediator mediator,
    ILogger<BatchEditStructureCommandHandler> logger)
    : IRequestHandler<BatchEditStructureCommand, StructureOperation>
{
    public async Task<StructureOperation> Handle(BatchEditStructureCommand request, CancellationToken ct)
    {
        logger.LogInformation("Executing structure batch with {Count} items for SiteId {SiteId}",
            request.Items?.Count ?? 0, request.SiteId);

        if (request.Items is null || request.Items.Count == 0 || request.Items.Count > 50)
            throw new StructureException("INVALID_STRUCTURE", "A batch must contain 1-50 items.");

        // Security check H-2: If any batch item performs markBackup, check BACKUP_MARK authority
        var requiredPermission = request.Items.Any(i => string.Equals(i.Action, "markBackup", StringComparison.OrdinalIgnoreCase))
            ? "BACKUP_MARK"
            : "PARKING_MANAGE";

        await scopeResolver.ResolveAsync(request.TenantId, ct, requiredPermission);

        var edit = new StructureEdit("batch", BatchItems: request.Items
            .Select(item => new StructureBatchItem(item.ResourceId, item.Action, item.ExpectedUpdatedAt))
            .ToArray());

        return await mediator.Send(new ExecuteStructureOperationCommand(
            request.SiteId, request.IdempotencyKey, edit, request.TenantId), ct);
    }
}
