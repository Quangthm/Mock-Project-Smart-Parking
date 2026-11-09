using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.ForceReleaseStructureHolds;

public sealed class ForceReleaseStructureHoldsCommandHandler(IStructureRepository repository, IOperationsConcurrency guards, IUnitOfWork work, TimeProvider clock)
    : IRequestHandler<ForceReleaseStructureHoldsCommand>
{
    public async Task Handle(ForceReleaseStructureHoldsCommand request, CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty || request.SiteId == Guid.Empty) throw new InvalidStructureEditException("Tenant and site must be non-empty.");
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var hold = await repository.HoldAsync(request.SiteId, ct);
        if (hold is null) return;
        if (hold.TenantId != request.TenantId) throw new HoldConflictException();
        repository.AddReceipt(new() { Token = hold.Token, TenantId = request.TenantId, SiteId = request.SiteId,
            Outcome = JsonSerializer.Serialize(new { status = "COMPENSATED_ABORT", reason = "Force released by saga compensation" }), CompletedAt = clock.GetUtcNow() });
        repository.RemoveHold(hold);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
