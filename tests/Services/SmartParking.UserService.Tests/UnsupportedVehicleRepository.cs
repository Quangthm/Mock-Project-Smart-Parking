using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;

namespace SmartParking.UserService.Tests;

// Non-vehicle HTTP fixtures have no database. Fail explicitly if a vehicle route is accidentally used.
// Vehicle endpoints are covered by VehicleRegistrationTests using the production PostgreSQL adapter.
internal sealed class UnsupportedVehicleRepository : IVehicleRepository
{
    public Task<Guid[]> LockActiveDriverAccountsAsync(Guid userId, CancellationToken ct) =>
        throw new NotSupportedException("Use the PostgreSQL vehicle fixture.");
    public Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid[] accountIds, CancellationToken ct) =>
        throw new NotSupportedException("Use the PostgreSQL vehicle fixture.");
    public Task<RegisteredVehicle?> FindForUpdateAsync(Guid id, Guid[] accountIds, CancellationToken ct) =>
        throw new NotSupportedException("Use the PostgreSQL vehicle fixture.");
    public void Add(RegisteredVehicle vehicle) =>
        throw new NotSupportedException("Use the PostgreSQL vehicle fixture.");
}
