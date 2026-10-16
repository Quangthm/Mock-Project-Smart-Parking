using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Persistence;

public interface IVehicleRepository
{
    // Call inside the use case transaction; locks protect eligibility until commit.
    Task<Guid[]> LockActiveDriverAccountsAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid[] accountIds, CancellationToken ct);
    Task<RegisteredVehicle?> FindForUpdateAsync(Guid id, Guid[] accountIds, CancellationToken ct);
    void Add(RegisteredVehicle vehicle);
}
