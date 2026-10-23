using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Persistence.Adapters;

public class TariffPersistenceAdapter : IParkingTariffPort
{
    private readonly PaymentDbContext _dbContext;

    public TariffPersistenceAdapter(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PricingPolicySnapshot?> GetActivePolicyAsync(
        Guid tenantId,
        Guid siteId,
        PricingVehicleType vehicleType,
        CancellationToken cancellationToken = default)
    {
        var vehicleTypeStr = vehicleType.ToString();

        var tariff = await _dbContext.Tariffs
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId &&
                        t.SiteId == siteId &&
                        t.VehicleType == vehicleTypeStr &&
                        t.IsActive &&
                        t.DeletedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (tariff == null)
        {
            return null;
        }

        var latestRevision = await _dbContext.PolicyRevisions
            .AsNoTracking()
            .Where(p => p.PolicyId == tariff.Id && p.PolicyType == "TARIFF")
            .OrderByDescending(p => p.RevisionNumber)
            .Select(p => p.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        int policyVersion = latestRevision > 0 ? latestRevision : 1;

        return TariffSnapshotParser.Parse(tariff, policyVersion);
    }
}
