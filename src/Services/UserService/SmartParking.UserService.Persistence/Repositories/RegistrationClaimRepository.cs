using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Persistence.Repositories;

public sealed class RegistrationClaimRepository(AppDbContext db) : IRegistrationClaimRepository
{
    public Task<VehicleRegistrationClaim?> FindPendingAsync(Guid accountId, string plate, CancellationToken ct) =>
        db.VehicleRegistrationClaims.AsNoTracking().SingleOrDefaultAsync(c => c.AccountId == accountId
            && c.CanonicalPlate == plate && c.Status == "PENDING_DISPUTE", ct);
    public async Task<IReadOnlyList<VehicleRegistrationClaim>> ListPendingAsync(Guid accountId, CancellationToken ct) =>
        await db.VehicleRegistrationClaims.AsNoTracking().Where(c => c.AccountId == accountId && c.Status == "PENDING_DISPUTE")
            .OrderBy(c => c.CreatedAt).ToArrayAsync(ct);
    public Task<bool> OwnsImageAsync(Guid accountId, Guid claimId, Guid assetId, CancellationToken ct) =>
        db.VehicleRegistrationClaims.AnyAsync(c => c.Id == claimId && c.AccountId == accountId
            && c.ImageAssetId == assetId && c.Status == "PENDING_DISPUTE", ct);
    public void Add(VehicleRegistrationClaim claim) => db.VehicleRegistrationClaims.Add(claim);
}
