using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;

namespace SmartParking.UserService.Tests;

// Nonvehicle HTTP fixtures fail explicitly if a vehicle persistence operation is used.
internal sealed class UnsupportedVehicleRepository : IVehicleRepository, IRegistrationClaimRepository
{
    public Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid accountId, CancellationToken ct) => throw new NotSupportedException();
    public Task<RegisteredVehicle?> FindForUpdateAsync(Guid id, Guid accountId, CancellationToken ct) => throw new NotSupportedException();
    public Task<RegisteredVehicle?> FindOwnedAsync(Guid id, Guid accountId, bool forUpdate, CancellationToken ct) => throw new NotSupportedException();
    public Task LockPlateAsync(string plate, CancellationToken ct) => throw new NotSupportedException();
    public Task<RegisteredVehicle?> FindActiveAsync(string plate, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasHistoryAsync(string plate, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> TryAddAsync(RegisteredVehicle vehicle, CancellationToken ct) => throw new NotSupportedException();
    public Task<VehicleBinding?> ResolveBindingAsync(Guid accountId, Guid vehicleId, CancellationToken ct) => throw new NotSupportedException();
    public Task<VehicleRegistrationClaim?> FindPendingAsync(Guid accountId, string plate, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<VehicleRegistrationClaim>> ListPendingAsync(Guid accountId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> OwnsImageAsync(Guid accountId, Guid claimId, Guid assetId, CancellationToken ct) => throw new NotSupportedException();
    public void Add(VehicleRegistrationClaim claim) => throw new NotSupportedException();
}
