using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.DTOs;

namespace SmartParking.FacilityService.Tests;

internal sealed class AdminFacilityDirectoryTestStore : IFacilityDirectoryStore
{
    public int Reads { get; private set; }

    public Task<AdminFacilityDirectoryDto> GetAdminDirectoryAsync(CancellationToken ct)
    {
        Reads++;
        var tenant = new AdminTenantOptionDto(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "DEMO-OWNER", "SmartPark Demo Owner", "ACTIVE");
        return Task.FromResult(new AdminFacilityDirectoryDto([tenant],
            [new(Guid.Parse("22222222-2222-2222-2222-222222222222"), tenant.Id,
                "DEMO-HQ", "SmartPark Demo Site", "ACTIVE", true)]));
    }

    public Task<bool> ProvisionTenantAsync(Guid id, TenantBody body, CancellationToken ct)
        => throw new InvalidOperationException("Directory read must not write tenants.");

    public Task<IReadOnlyList<ValidatedSite>> ValidateSitesAsync(SiteValidation body, CancellationToken ct)
        => throw new InvalidOperationException("Directory read must not invoke another use case.");
}
