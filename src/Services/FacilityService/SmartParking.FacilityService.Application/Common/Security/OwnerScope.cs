namespace SmartParking.FacilityService.Application.Common.Security;

public sealed record OwnerScope(Guid UserId, Guid TenantId);
