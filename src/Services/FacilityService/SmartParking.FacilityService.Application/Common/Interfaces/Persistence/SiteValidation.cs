namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public sealed record SiteValidation(Guid[] SiteIds, Guid[] TenantIds);
