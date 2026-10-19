using SmartParking.ParkingService.Domain;
namespace SmartParking.ParkingService.Application;
public sealed record TenantBody(string Name, string Email, string Phone);
public sealed record SiteValidation(Guid[] SiteIds, Guid[] TenantIds);
public sealed record ValidatedSite(Guid Id, Guid TenantId);
public interface IFacilityDirectoryStore
{
    Task<bool> ProvisionTenantAsync(Guid id, TenantBody body, CancellationToken ct);
    Task<IReadOnlyList<ValidatedSite>> ValidateSitesAsync(SiteValidation body, CancellationToken ct);
}
public sealed class FacilityDirectoryService(IFacilityDirectoryStore store)
{
    public Task<bool> ProvisionTenantAsync(Guid id, TenantBody body, CancellationToken ct)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(body.Name) || body.Name.Length > 255 ||
            string.IsNullOrWhiteSpace(body.Email) || body.Email.Length > 255 || string.IsNullOrWhiteSpace(body.Phone) || body.Phone.Length > 20)
            throw new StructureException("INVALID_TENANT", "Provide a valid tenant identity and contact.");
        return store.ProvisionTenantAsync(id, body, ct);
    }
    public Task<IReadOnlyList<ValidatedSite>> ValidateSitesAsync(SiteValidation body, CancellationToken ct)
    {
        if (body.SiteIds is null || body.TenantIds is null || body.SiteIds.Length > 100 || body.TenantIds.Length > 100)
            throw new StructureException("INVALID_SITE_QUERY", "Provide at most 100 site and tenant identifiers.");
        return store.ValidateSitesAsync(body, ct);
    }
}
