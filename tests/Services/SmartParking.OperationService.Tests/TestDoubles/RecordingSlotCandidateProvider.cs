using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Tests.TestDoubles;

// Deterministic bay catalogue for the emergency reallocation step.
internal sealed class RecordingSlotCandidateProvider : IEmergencyReallocationSlotProvider
{
    public IReadOnlyList<FacilitySlotCandidate> Candidates { get; set; } = [];
    public int ListCalls { get; private set; }

    public Task<IReadOnlyList<FacilitySlotCandidate>> ListSiteSlotCandidatesAsync(
        Guid siteId, CancellationToken ct)
    {
        ListCalls++;
        return Task.FromResult(Candidates);
    }
}