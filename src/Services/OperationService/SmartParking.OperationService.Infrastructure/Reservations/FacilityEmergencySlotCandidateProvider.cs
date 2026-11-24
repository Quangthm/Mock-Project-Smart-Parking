using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

// Facility exposes no service-to-service slot catalogue: its layout endpoints are owner/operator
// authenticated, and the only internal facility read is the single-slot dependency endpoint.
// Returning an empty catalogue is therefore the honest answer - the reconciliation cancels with
// FACILITY_FAILURE instead of moving a driver to a bay that nobody verified. Replace this adapter
// (not the handler) once Facility publishes a service-key slot catalogue.
public sealed class FacilityEmergencySlotCandidateProvider(
    ILogger<FacilityEmergencySlotCandidateProvider>? logger = null) : IEmergencyReallocationSlotProvider
{
    public Task<IReadOnlyList<FacilitySlotCandidate>> ListSiteSlotCandidatesAsync(
        Guid siteId, CancellationToken ct)
    {
        logger?.LogWarning(
            "No service-to-service slot catalogue is available; emergency reconciliation for site {SiteId} cannot reallocate.",
            siteId);
        return Task.FromResult<IReadOnlyList<FacilitySlotCandidate>>([]);
    }
}