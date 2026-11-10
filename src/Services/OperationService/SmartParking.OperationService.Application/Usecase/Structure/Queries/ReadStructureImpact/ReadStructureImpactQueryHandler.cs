using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Queries.ReadStructureImpact;

public sealed class ReadStructureImpactQueryHandler(StructureSafetyService service)
    : IRequestHandler<ReadStructureImpactQuery, StructureImpactResponse>
{
    public Task<StructureImpactResponse> Handle(ReadStructureImpactQuery request, CancellationToken ct) =>
        service.ImpactAsync(request.TenantId, request.SiteId, ct);
}
