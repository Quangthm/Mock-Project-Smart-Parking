using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Queries.ValidateSites;

public sealed class ValidateSitesQueryHandler(
    IFacilityDirectoryStore directory,
    ILogger<ValidateSitesQueryHandler> logger)
    : IRequestHandler<ValidateSitesQuery, IReadOnlyList<ValidatedSite>>
{
    public async Task<IReadOnlyList<ValidatedSite>> Handle(ValidateSitesQuery request, CancellationToken ct)
    {
        logger.LogInformation("Validating sites and tenants");
        if (request.Body.SiteIds is null || request.Body.TenantIds is null ||
            request.Body.SiteIds.Length > 100 || request.Body.TenantIds.Length > 100)
        {
            throw new StructureException("INVALID_SITE_QUERY", "Provide at most 100 site and tenant identifiers.");
        }

        return await directory.ValidateSitesAsync(request.Body, ct);
    }
}
