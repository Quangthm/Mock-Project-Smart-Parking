namespace UserService.Application.Common.Interfaces.Services;

// Transport contract only. UserService never reads ParkingService persistence.
public sealed record DirectorySite(Guid Id, Guid TenantId);
public interface IParkingDirectory
{
    Task ProvisionTenantAsync(Guid tenantId, string name, string email, string phone, CancellationToken ct);
    Task<IReadOnlyList<DirectorySite>> ActiveSitesAsync(Guid[] siteIds, Guid[] tenantIds, CancellationToken ct);
}
