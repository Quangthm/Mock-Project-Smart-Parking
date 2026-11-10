using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Lots.Queries.ListParkingLots;

public sealed class ListParkingLotsQueryHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IParkingStructureRepository repository,
    ILogger<ListParkingLotsQueryHandler> logger)
    : IRequestHandler<ListParkingLotsQuery, IReadOnlyList<SiteProfile>>
{
    public async Task<IReadOnlyList<SiteProfile>> Handle(ListParkingLotsQuery request, CancellationToken ct)
    {
        logger.LogInformation("Listing parking lots for TenantId {TenantId}", request.TenantId);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");

        return await repository.ListAsync(owner, ct);
    }
}
