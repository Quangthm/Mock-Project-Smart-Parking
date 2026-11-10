using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Persistence.Repositories;

public sealed class VehicleRepository(AppDbContext db) : IVehicleRepository
{
    public async Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid accountId, CancellationToken ct) =>
        await db.RegisteredVehicles.AsNoTracking()
            .Where(v => v.AccountId != null && v.AccountId == accountId && v.DeletedAt == null && (v.VehicleType == "CAR" || v.VehicleType == "MOTORCYCLE") && v.CanonicalPlate != "")
            .OrderBy(v => v.CreatedAt).ToArrayAsync(ct);

    public async Task<RegisteredVehicle?> FindForUpdateAsync(Guid id, Guid accountId, CancellationToken ct)
    {
        var vehicle = await db.RegisteredVehicles.FromSqlInterpolated(
            $"SELECT * FROM vehicles WHERE id={id} AND account_id={accountId} AND deleted_at IS NULL AND vehicle_type IN ('CAR','MOTORCYCLE') AND normalized_plate<>'' FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (vehicle is not null) await db.Entry(vehicle).ReloadAsync(ct);
        return vehicle;
    }

    // Registration writers serialize by canonical plate. The unique index remains the final guard.
    public async Task LockPlateAsync(string plate, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({plate}, 243))", ct);
    public Task<RegisteredVehicle?> FindActiveAsync(string plate, CancellationToken ct) =>
        db.RegisteredVehicles.AsNoTracking().SingleOrDefaultAsync(v => v.CanonicalPlate == plate && v.DeletedAt == null, ct);
    public Task<bool> HasHistoryAsync(string plate, CancellationToken ct) =>
        db.RegisteredVehicles.AnyAsync(v => v.CanonicalPlate == plate && v.DeletedAt != null, ct);
    public async Task<bool> TryAddAsync(RegisteredVehicle vehicle, CancellationToken ct)
    {
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO vehicles (id, account_id, original_plate, normalized_plate, vehicle_type, created_at, updated_at)
            VALUES ({vehicle.Id}, {vehicle.AccountId}, {vehicle.RawPlate}, {vehicle.CanonicalPlate}, {vehicle.VehicleType}, {vehicle.CreatedAt}, {vehicle.UpdatedAt})
            ON CONFLICT (normalized_plate) WHERE deleted_at IS NULL DO NOTHING
            """, ct);
        if (count == 1) db.RegisteredVehicles.Attach(vehicle);
        return count == 1;
    }
    public Task<VehicleBinding?> ResolveBindingAsync(Guid accountId, Guid vehicleId, CancellationToken ct) =>
        db.RegisteredVehicles.AsNoTracking().Where(v => v.Id == vehicleId && v.AccountId == accountId && v.DeletedAt == null
            && v.CanonicalPlate != "" && (v.VehicleType == "CAR" || v.VehicleType == "MOTORCYCLE"))
            .Select(v => new VehicleBinding(v.Id, accountId, v.VehicleType, v.BindingRevision)).SingleOrDefaultAsync(ct);

    public async Task<RegisteredVehicle?> FindOwnedAsync(Guid id, Guid accountId, bool forUpdate, CancellationToken ct)
    {
        if (!forUpdate) return await db.RegisteredVehicles.AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == id && v.AccountId == accountId && v.DeletedAt == null, ct);
        var vehicle = await db.RegisteredVehicles.FromSqlInterpolated(
            $"SELECT * FROM vehicles WHERE id={id} AND account_id={accountId} AND deleted_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct);
        if (vehicle is not null) await db.Entry(vehicle).ReloadAsync(ct);
        return vehicle;
    }

}
