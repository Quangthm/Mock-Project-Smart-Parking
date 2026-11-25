namespace SmartParking.FacilityService.Application.DTOs;

public sealed record AdminFacilityDirectoryDto(
    IReadOnlyList<AdminTenantOptionDto> Tenants,
    IReadOnlyList<AdminSiteOptionDto> Sites);
