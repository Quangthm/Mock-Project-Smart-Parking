using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Persistence;

public interface IRegistrationClaimRepository
{
    Task<VehicleRegistrationClaim?> FindPendingAsync(Guid accountId, string canonicalPlate, CancellationToken ct);
    Task<IReadOnlyList<VehicleRegistrationClaim>> ListPendingAsync(Guid accountId, CancellationToken ct);
    Task<bool> OwnsImageAsync(Guid accountId, Guid claimId, Guid assetId, CancellationToken ct);
    void Add(VehicleRegistrationClaim claim);
}
