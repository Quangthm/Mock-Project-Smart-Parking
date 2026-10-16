using SmartParking.UserService.Domain.Entities;
using UserService.Application.DTOs;

namespace UserService.Application.Common.Interfaces.Services;

public interface IVehicleRegistration
{
    Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid actor, CancellationToken ct);
    Task<RegisteredVehicle> SaveAsync(Guid actor, Guid? id, VehicleDto body, CancellationToken ct);
}
