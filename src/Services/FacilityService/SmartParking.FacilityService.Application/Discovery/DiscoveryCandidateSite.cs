namespace SmartParking.FacilityService.Application.Discovery;

public sealed record DiscoveryCandidateSite(Guid SiteId, Guid TenantId, Guid ConfigurationVersion, bool Complete,
    IReadOnlyList<DiscoveryCandidateSlot> Slots);
