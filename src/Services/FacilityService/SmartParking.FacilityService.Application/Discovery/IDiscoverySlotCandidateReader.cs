namespace SmartParking.FacilityService.Application.Discovery;

public interface IDiscoverySlotCandidateReader
{
    Task<DiscoveryCandidateBatch> ReadAsync(ReadDiscoverySlotCandidatesQuery query, CancellationToken ct);
}
