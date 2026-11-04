using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperatorLayout;

public sealed class GetOperatorLayoutQueryHandler(
    IOperatorSiteAuthorizer operators,
    IParkingStructureRepository repository,
    IStructureCommitments commitments,
    ILogger<GetOperatorLayoutQueryHandler> logger)
    : IRequestHandler<GetOperatorLayoutQuery, OperatorStructureResponse>
{
    public async Task<OperatorStructureResponse> Handle(GetOperatorLayoutQuery request, CancellationToken ct)
    {
        logger.LogInformation("Reading operator layout for site {SiteId}", request.SiteId);
        var actor = await operators.ScopeAsync(request.SiteId, ct);
        // Read-only dashboard projection: never acquires a structure-edit hold.
        var snapshot = await commitments.ReadSnapshotAsync(actor.TenantId, request.SiteId, ct);
        var structure = await repository.ReadAsync(actor, request.SiteId, snapshot, ct);
        return new OperatorStructureResponse(
            structure.Site,
            structure.Units,
            structure.Slots,
            structure.Paths,
            structure.BackupPolicies,
            structure.CapacityViews);
    }
}
