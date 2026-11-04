using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.DTOs;

namespace SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperatorLayout;

public sealed class GetOperatorLayoutQueryHandler(
    ISlotOperatorAuthorizer operatorAuthorizer,
    OperatorLayoutService operatorLayoutService,
    ILogger<GetOperatorLayoutQueryHandler> logger)
    : IRequestHandler<GetOperatorLayoutQuery, OperatorStructureResponse>
{
    public async Task<OperatorStructureResponse> Handle(GetOperatorLayoutQuery request, CancellationToken ct)
    {
        logger.LogInformation("Reading operator layout for site {SiteId}", request.SiteId);
        await operatorAuthorizer.ScopeAsync(request.SiteId, ct);
        var structure = await operatorLayoutService.ReadAsync(request.SiteId, ct);
        return new OperatorStructureResponse(
            structure.Site,
            structure.Units,
            structure.Slots,
            structure.Paths,
            structure.BackupPolicies,
            structure.CapacityViews);
    }
}
