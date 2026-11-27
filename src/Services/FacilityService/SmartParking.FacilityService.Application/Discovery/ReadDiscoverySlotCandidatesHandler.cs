using MediatR;

namespace SmartParking.FacilityService.Application.Discovery;

public sealed class ReadDiscoverySlotCandidatesHandler(IDiscoverySlotCandidateReader reader)
    : IRequestHandler<ReadDiscoverySlotCandidatesQuery, DiscoveryCandidateBatch>
{
    public Task<DiscoveryCandidateBatch> Handle(ReadDiscoverySlotCandidatesQuery query, CancellationToken ct) =>
        reader.ReadAsync(query, ct);
}
