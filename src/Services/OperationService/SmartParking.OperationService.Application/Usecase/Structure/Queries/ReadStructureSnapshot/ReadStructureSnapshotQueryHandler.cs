using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartParking.OperationService.Application.Usecase.Structure.Queries.ReadStructureSnapshot;

public sealed class ReadStructureSnapshotQueryHandler(IStructureRepository repository, IOperationsConcurrency guards, IUnitOfWork work, TimeProvider clock)
    : IRequestHandler<ReadStructureSnapshotQuery, HoldSnapshot>
{
    public async Task<HoldSnapshot> Handle(ReadStructureSnapshotQuery request, CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty || request.SiteId == Guid.Empty) throw new InvalidStructureEditException("Tenant and site must be non-empty.");
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var result = await repository.SnapshotAsync(request.TenantId, request.SiteId, clock.GetUtcNow(), ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
