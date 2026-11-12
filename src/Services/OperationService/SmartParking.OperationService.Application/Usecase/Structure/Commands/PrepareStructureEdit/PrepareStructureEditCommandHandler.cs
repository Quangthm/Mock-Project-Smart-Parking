using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.PrepareStructureEdit;

public sealed class PrepareStructureEditCommandHandler(IStructureRepository repository, IOperationsConcurrency guards, IUnitOfWork work, TimeProvider clock)
    : IRequestHandler<PrepareStructureEditCommand, HoldSnapshot>
{
    public async Task<HoldSnapshot> Handle(PrepareStructureEditCommand request, CancellationToken ct)
    {
        StructureEditPolicy.ValidateScope(request.TenantId, request.SiteId, request.Token);
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var state = await repository.StateAsync(request.SiteId, ct);
        if (state is not null && state.TenantId != request.TenantId) throw new HoldConflictException();
        if (await repository.ReceiptAsync(request.Token, ct) is not null) throw new HoldConflictException();
        var hold = await repository.HoldAsync(request.SiteId, ct);
        if (hold is null)
            repository.AddHold(new() { SiteId = request.SiteId, TenantId = request.TenantId, Token = request.Token, CreatedAt = clock.GetUtcNow() });
        else if (hold.TenantId != request.TenantId || hold.Token != request.Token) throw new HoldConflictException();
        var result = await repository.SnapshotAsync(request.TenantId, request.SiteId, clock.GetUtcNow(), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
