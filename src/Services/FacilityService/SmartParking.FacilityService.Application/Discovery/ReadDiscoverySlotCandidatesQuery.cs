using MediatR;

namespace SmartParking.FacilityService.Application.Discovery;

public sealed record ReadDiscoverySlotCandidatesQuery(DiscoverySiteRequest[] Sites, string VehicleType,
    DateTimeOffset At, DateTimeOffset? StartAt, DateTimeOffset? EndAt) : IRequest<DiscoveryCandidateBatch>;
