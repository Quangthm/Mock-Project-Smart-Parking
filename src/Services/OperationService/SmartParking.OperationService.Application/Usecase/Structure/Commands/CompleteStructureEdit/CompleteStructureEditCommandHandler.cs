using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.CompleteStructureEdit;

public sealed class CompleteStructureEditCommandHandler(IStructureRepository repository, IOperationsConcurrency guards, IUnitOfWork work, TimeProvider clock)
    : IRequestHandler<CompleteStructureEditCommand>
{
    public async Task Handle(CompleteStructureEditCommand request, CancellationToken ct)
    {
        StructureEditPolicy.ValidateScope(request.TenantId, request.SiteId, request.Token);
        StructureEditPolicy.ValidateOutcome(request.Outcome);
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var payload = JsonSerializer.Serialize(request.Outcome, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var receipt = await repository.ReceiptAsync(request.Token, ct);
        if (receipt is not null)
        {
            if (receipt.TenantId != request.TenantId || receipt.SiteId != request.SiteId ||
                !JsonNode.DeepEquals(JsonNode.Parse(receipt.Outcome), JsonNode.Parse(payload))) throw new HoldConflictException();
            return;
        }
        var hold = await repository.HoldAsync(request.SiteId, ct);
        if (hold is null && request.Outcome is not null || hold is not null &&
            (hold.TenantId != request.TenantId || hold.Token != request.Token)) throw new HoldConflictException();
        if (hold is not null)
        {
            repository.RemoveHold(hold);
            await work.SaveChangesAsync(ct);
        }
        if (request.Outcome is { } outcome)
        {
            var state = await repository.StateAsync(request.SiteId, ct);
            if (state is not null && state.TenantId != request.TenantId) throw new HoldConflictException();
            if (state is null) repository.AddState(new() { SiteId = request.SiteId, TenantId = request.TenantId, Active = outcome.SiteActive });
            else state.Active = outcome.SiteActive;
            await work.SaveChangesAsync(ct);
            foreach (var pool in await repository.PoolsAsync(request.TenantId, request.SiteId, ct))
            {
                var capacity = outcome.Capacities.FirstOrDefault(c => c.UnitId == pool.SpatialUnitId && c.VehicleType == pool.VehicleType);
                pool.ApplyStructureCapacity(capacity?.Capacity ?? 0, capacity?.Backup ?? 0, clock.GetUtcNow());
            }
        }
        repository.AddReceipt(new() { Token = request.Token, TenantId = request.TenantId, SiteId = request.SiteId,
            Outcome = payload, CompletedAt = clock.GetUtcNow() });
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
