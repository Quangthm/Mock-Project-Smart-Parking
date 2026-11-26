using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class SubscribedVehicleRepository(ReservationDbContext db) : ISubscribedVehicleRepository
{
    public async Task<bool> IsCoveredAsync(
        Guid tenantId, Guid siteId, string plateNumber, DateTimeOffset at, CancellationToken ct)
    {
        var normalizedPlate = GateRules.NormalizePlate(plateNumber);
        if (normalizedPlate.Length == 0) return false;

        var activePlates = await db.SubscribedVehicles.AsNoTracking()
            .Where(vehicle => vehicle.TenantId == tenantId && vehicle.SiteId == siteId &&
                vehicle.ActiveAt <= at && vehicle.ExpireAt > at)
            .Select(vehicle => vehicle.LicensePlate)
            .ToListAsync(ct);

        return activePlates.Any(plate => GateRules.NormalizePlate(plate) == normalizedPlate);
    }
}
