using SmartParking.FacilityService.Application.DTOs;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public interface IFacilityDirectoryStore
{
    Task<AdminFacilityDirectoryDto> GetAdminDirectoryAsync(CancellationToken ct);
    Task<bool> ProvisionTenantAsync(Guid id, TenantBody body, CancellationToken ct);
    Task<IReadOnlyList<ValidatedSite>> ValidateSitesAsync(SiteValidation body, CancellationToken ct);
}
