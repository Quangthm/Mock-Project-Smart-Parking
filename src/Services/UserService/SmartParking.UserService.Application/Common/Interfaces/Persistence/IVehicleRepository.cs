using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Persistence;

public interface IVehicleRepository
{
    Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid accountId, CancellationToken ct);
    Task<RegisteredVehicle?> FindForUpdateAsync(Guid id, Guid accountId, CancellationToken ct);
    Task<RegisteredVehicle?> FindOwnedAsync(Guid id, Guid accountId, bool forUpdate, CancellationToken ct);
    Task LockPlateAsync(string canonicalPlate, CancellationToken ct);
    Task<RegisteredVehicle?> FindActiveAsync(string canonicalPlate, CancellationToken ct);
    Task<bool> HasHistoryAsync(string canonicalPlate, CancellationToken ct);
    // Targeted insert participates in the caller's IUnitOfWork transaction; it never commits.
    Task<bool> TryAddAsync(RegisteredVehicle vehicle, CancellationToken ct);
    Task<VehicleBinding?> ResolveBindingAsync(Guid accountId, Guid vehicleId, CancellationToken ct);
}

public sealed record VehicleBinding(Guid VehicleId, Guid AccountId, string VehicleType, long Revision);
