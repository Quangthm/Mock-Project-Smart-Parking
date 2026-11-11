using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Helpers;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Operations.Commands.ExecuteStructureOperation;

public sealed class ExecuteStructureOperationCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IStructureOperationStore store,
    IStructureImpactPlanner planner,
    IMediator mediator,
    IOwnerAuthorizer owners,
    ILogger<ExecuteStructureOperationCommandHandler> logger)
    : IRequestHandler<ExecuteStructureOperationCommand, StructureOperation>
{
    public async Task<StructureOperation> Handle(ExecuteStructureOperationCommand request, CancellationToken ct)
    {
        logger.LogInformation("Executing structure operation {OperationKey} for site {SiteId}",
            request.IdempotencyKey, request.SiteId);

        var requiredPermission = request.Edit.Action == "markBackup" ||
            (request.Edit.BatchItems?.Any(i => string.Equals(i.Action, "markBackup", StringComparison.OrdinalIgnoreCase)) ?? false)
            ? "BACKUP_MARK"
            : "PARKING_MANAGE";

        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct, requiredPermission);

        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current Owner access is required.");

        if (request.IdempotencyKey == Guid.Empty)
            throw new StructureException("INVALID_STRUCTURE", "Provide an idempotency key.");

        if (request.Edit.Action is not ("removeSlot" or "removeUnit" or "capacity" or "active" or "moveSlot" or "configureSlot" or "batch"))
            throw new StructureException("INVALID_STRUCTURE", "Unsupported impact operation.");

        var operation = await store.BeginAsync(owner, request.SiteId, request.IdempotencyKey, request.Edit, ct);
        await using var gate = await store.LockAsync(operation.Id, ct);
        operation = await store.ReadAsync(owner, request.SiteId, operation.Id, ct);
        if (operation.Status == "completed") return operation;

        // A previous attempt may have committed physically but lost its response.
        // Recovery publishes the durable outcome before we plan any new changes.
        await mediator.Send(new RecoverAuthorizedStructureCommand(owner, request.SiteId), ct);
        operation = await store.ReadAsync(owner, request.SiteId, operation.Id, ct);
        if (operation.Status == "completed") return operation;

        var plan = await planner.PlanAsync(owner, request.SiteId, operation.Id, request.Edit, ct);
        if (plan.Status != "resolved")
        {
            return await store.UpdateAsync(
                owner, operation.Id, plan.Status == "failed" ? "failed" : "pending",
                plan.Reason, plan.Impact, ct);
        }

        await store.UpdateAsync(
            owner, operation.Id, "pending",
            "Impact resolved; awaiting physical commit and projection acknowledgement.",
            plan.Impact, ct);

        try
        {
            // The Domain rechecks the change under the normal hold and local row
            // locks. A planner response by itself does not authorize eviction.
            await using (var session = await mediator.Send(new BeginStructureEditCommand(owner, request.SiteId, OperationId: operation.Id), ct))
            {
                StructureEditHandler.Apply(session.Structure, request.Edit);
                await session.CommitAsync(ct);
            }

            // Synchronization acknowledgement already completed this operation
            // atomically. Updating it again would race with recovery.
            return await store.ReadAsync(owner, request.SiteId, operation.Id, ct);
        }
        catch (StructureBatchException error)
        {
            // The operation is marked failed, but the per-item payload must reach the caller: the
            // middleware renders the contract's 409 body, and nothing was physically applied.
            await store.UpdateAsync(owner, operation.Id, "failed", error.Message, plan.Impact, ct);
            throw;
        }
        catch (StructureException error)
        {
            var status = error.Code is "STRUCTURE_IN_USE" or "STRUCTURE_BUSY" or "COMMITMENTS_UNAVAILABLE"
                ? "pending" : "failed";
            return await store.UpdateAsync(
                owner, operation.Id, status, error.Message, plan.Impact, ct);
        }
    }
}
