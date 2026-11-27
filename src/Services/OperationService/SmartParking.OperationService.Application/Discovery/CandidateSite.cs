namespace SmartParking.OperationService.Application.Discovery;

public sealed record CandidateSite(Guid SiteId, Guid TenantId, Guid ConfigurationVersion, bool Complete, CandidateSlot[] Slots);
