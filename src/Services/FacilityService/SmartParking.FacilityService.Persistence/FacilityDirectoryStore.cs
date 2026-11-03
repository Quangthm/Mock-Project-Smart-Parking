using Microsoft.EntityFrameworkCore;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
namespace SmartParking.FacilityService.Persistence;
public sealed class FacilityDirectoryStore(ParkingDbContext database, IUnitOfWork unitOfWork) : IFacilityDirectoryStore
{
    public async Task<bool> ProvisionTenantAsync(Guid id, TenantBody body, CancellationToken ct)
    {
        if (!await database.Tenants.AnyAsync(tenant => tenant.Id == id, ct))
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            database.Tenants.Add(new TenantEntity
            {
                Id = id,
                Code = "T-" + id.ToString("N"),
                Name = body.Name,
                Email = body.Email,
                Phone = body.Phone,
                Status = "ACTIVE"
            });
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        return await database.Tenants.AnyAsync(tenant =>
            tenant.Id == id &&
            tenant.Name == body.Name &&
            tenant.Email == body.Email &&
            tenant.Phone == body.Phone &&
            tenant.Status == "ACTIVE" &&
            tenant.DeletedAt == null, ct);
    }
    public async Task<IReadOnlyList<ValidatedSite>> ValidateSitesAsync(SiteValidation body, CancellationToken ct)
    {
        return await database.ParkingSites
            .AsNoTracking()
            .Where(site => body.SiteIds.Contains(site.Id) &&
                body.TenantIds.Contains(site.TenantId) &&
                site.Status == "ACTIVE" &&
                site.DeletedAt == null &&
                database.Tenants.Any(tenant => tenant.Id == site.TenantId &&
                    tenant.Status == "ACTIVE" && tenant.DeletedAt == null))
            .OrderBy(site => site.Id)
            .Select(site => new ValidatedSite(site.Id, site.TenantId))
            .ToArrayAsync(ct);
    }
}
