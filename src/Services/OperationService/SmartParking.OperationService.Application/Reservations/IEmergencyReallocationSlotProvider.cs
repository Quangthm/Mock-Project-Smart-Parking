namespace SmartParking.OperationService.Application.Reservations;

// Facility resolves which physical bays exist for a site. Reconciliation asks through this port
// so the handler never calls a concrete client and tests can supply a deterministic catalogue.
public interface IEmergencyReallocationSlotProvider
{
    Task<IReadOnlyList<FacilitySlotCandidate>> ListSiteSlotCandidatesAsync(
        Guid siteId, CancellationToken ct);
}