using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Queries.ReadStructureSnapshot;

public sealed class ReadStructureSnapshotQueryHandler(StructureSafetyService service)
    : IRequestHandler<ReadStructureSnapshotQuery, HoldSnapshot>
{
    public Task<HoldSnapshot> Handle(ReadStructureSnapshotQuery request, CancellationToken ct) =>
        service.ReadSnapshotAsync(request.TenantId, request.SiteId, ct);
}
