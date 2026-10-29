using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Persistence.Repositories;

public sealed class VehicleRepository(AppDbContext db) : IVehicleRepository
{
    public async Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid[] accountIds, CancellationToken ct) =>
        await db.RegisteredVehicles.AsNoTracking()
            .Where(v => v.AccountId != null && accountIds.Contains(v.AccountId.Value) && v.DeletedAt == null && (v.VehicleType == "CAR" || v.VehicleType == "MOTORCYCLE") && v.CanonicalPlate != "")
            .OrderBy(v => v.CreatedAt).ToArrayAsync(ct);

    public Task<RegisteredVehicle?> FindForUpdateAsync(Guid id, Guid[] accountIds, CancellationToken ct) =>
        db.RegisteredVehicles.FromSqlInterpolated(
            $"SELECT * FROM vehicles WHERE id={id} AND account_id=ANY({accountIds}) AND deleted_at IS NULL AND vehicle_type IN ('CAR','MOTORCYCLE') AND normalized_plate<>'' FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    public void Add(RegisteredVehicle vehicle) => db.RegisteredVehicles.Add(vehicle);
}
