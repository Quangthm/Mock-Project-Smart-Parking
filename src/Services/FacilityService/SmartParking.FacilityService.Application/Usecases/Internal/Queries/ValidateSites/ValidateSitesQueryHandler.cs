using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Queries.ValidateSites;

public sealed class ValidateSitesQueryHandler(
    FacilityDirectoryService directory,
    ILogger<ValidateSitesQueryHandler> logger)
    : IRequestHandler<ValidateSitesQuery, IReadOnlyList<ValidatedSite>>
{
    public async Task<IReadOnlyList<ValidatedSite>> Handle(ValidateSitesQuery request, CancellationToken ct)
    {
        logger.LogInformation("Validating sites and tenants");
        return await directory.ValidateSitesAsync(request.Body, ct);
    }
}
