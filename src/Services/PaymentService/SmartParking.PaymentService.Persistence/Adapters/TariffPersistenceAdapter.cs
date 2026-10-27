using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Persistence.Adapters;

public class TariffPersistenceAdapter : IParkingTariffPort
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<TariffPersistenceAdapter> _logger;

    public TariffPersistenceAdapter(PaymentDbContext dbContext, ILogger<TariffPersistenceAdapter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PricingPolicySnapshot?> GetActivePolicyAsync(
        Guid tenantId,
        Guid siteId,
        PricingVehicleType vehicleType,
        CancellationToken cancellationToken = default)
    {
        var vehicleTypeStr = vehicleType.ToString();

        _logger.LogDebug(
            "Querying active tariff for TenantId: {TenantId}, SiteId: {SiteId}, VehicleType: {VehicleType}",
            tenantId, siteId, vehicleTypeStr);

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
            _logger.LogWarning(
                "No active/non-deleted tariff found in PaymentDb for TenantId: {TenantId}, SiteId: {SiteId}, VehicleType: {VehicleType}",
                tenantId, siteId, vehicleTypeStr);
            return null;
        }

        var latestRevision = await _dbContext.PolicyRevisions
            .AsNoTracking()
            .Where(p => p.PolicyId == tariff.Id && p.PolicyType == "TARIFF")
            .OrderByDescending(p => p.RevisionNumber)
            .Select(p => p.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        int policyVersion = latestRevision > 0 ? latestRevision : 1;

        _logger.LogInformation(
            "Resolved active tariff from PaymentDb. TariffId: {TariffId}, Name: {TariffName}, PolicyVersion (Revision): {PolicyVersion}",
            tariff.Id, tariff.Name, policyVersion);

        return TariffSnapshotParser.Parse(tariff, policyVersion);
    }
}
