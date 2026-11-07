using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Media;

namespace UserService.Persistence.Media;

public sealed class MediaStore(AppDbContext db) : IMediaStore
{
    // Admission takes the existing current-access user/account guard first.
    public Task<MediaAsset?> FindByKeyAsync(Guid scopeId, string purpose, string key, CancellationToken ct) =>
        db.MediaAssets.FromSqlInterpolated($"SELECT * FROM media_assets WHERE scope_id={scopeId} AND purpose={purpose} AND idempotency_key={key} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

    public async Task<MediaAsset?> FindAsync(Guid id, bool forUpdate, CancellationToken ct)
    {
        if (!forUpdate) return await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        var asset = await db.MediaAssets.FromSqlInterpolated($"SELECT * FROM media_assets WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
        // An admission/earlier read may still be tracked. Refresh under the lock.
        if (asset is not null) await db.Entry(asset).ReloadAsync(ct);
        return asset;
    }
    public void Add(MediaAsset asset) => db.MediaAssets.Add(asset);
    public async Task<RegisteredVehicle?> VehicleAsync(Guid id, Guid accountId, bool forUpdate, CancellationToken ct)
    {
        if (!forUpdate) return await db.RegisteredVehicles.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id && v.AccountId == accountId && v.DeletedAt == null, ct);
        var vehicle = await db.RegisteredVehicles.FromSqlInterpolated($"SELECT * FROM vehicles WHERE id={id} AND account_id={accountId} AND deleted_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct);
        if (vehicle is not null) await db.Entry(vehicle).ReloadAsync(ct);
        return vehicle;
    }
    public async Task<User?> UserAsync(Guid id, bool forUpdate, CancellationToken ct)
    {
        if (!forUpdate) return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is not null) await db.Entry(user).ReloadAsync(ct);
        return user;
    }
    public Task<Guid[]> PendingAsync(DateTimeOffset now, int take, CancellationToken ct) => db.MediaAssets.AsNoTracking()
        .Where(a => a.State == "pending" && a.Attempts < 5 && a.DraftExpiresAt > now && a.RetainUntil > now
            && (a.LeaseUntil == null || a.LeaseUntil <= now) && (a.RetryAt == null || a.RetryAt <= now))
        .OrderBy(a => a.AdmittedAt).ThenBy(a => a.Id).Take(take).Select(a => a.Id).ToArrayAsync(ct);
}
