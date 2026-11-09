namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public sealed record TenantBody(string Name, string Email, string Phone);
public sealed record SiteValidation(Guid[] SiteIds, Guid[] TenantIds);
public sealed record ValidatedSite(Guid Id, Guid TenantId);

public interface IFacilityDirectoryStore
{
    Task<bool> ProvisionTenantAsync(Guid id, TenantBody body, CancellationToken ct);
    Task<IReadOnlyList<ValidatedSite>> ValidateSitesAsync(SiteValidation body, CancellationToken ct);
}
