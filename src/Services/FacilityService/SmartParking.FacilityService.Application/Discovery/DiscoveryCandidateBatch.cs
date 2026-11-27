namespace SmartParking.FacilityService.Application.Discovery;

public sealed record DiscoveryCandidateBatch(DateTimeOffset AsOf, DateTimeOffset At, DateTimeOffset? StartAt,
    DateTimeOffset? EndAt, IReadOnlyList<DiscoveryCandidateSite> Items);
