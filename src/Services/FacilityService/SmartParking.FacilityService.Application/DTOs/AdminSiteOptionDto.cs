namespace SmartParking.FacilityService.Application.DTOs;

public sealed record AdminSiteOptionDto(Guid Id, Guid TenantId, string Code, string Name, string Status, bool IsActive);
