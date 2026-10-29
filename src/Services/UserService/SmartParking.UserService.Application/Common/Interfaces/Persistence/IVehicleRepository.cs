using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Persistence;

public interface IVehicleRepository
{
    Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid[] accountIds, CancellationToken ct);
    Task<RegisteredVehicle?> FindForUpdateAsync(Guid id, Guid[] accountIds, CancellationToken ct);
    void Add(RegisteredVehicle vehicle);
}
