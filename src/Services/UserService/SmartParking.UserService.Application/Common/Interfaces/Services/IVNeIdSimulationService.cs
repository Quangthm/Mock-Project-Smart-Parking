using SmartParking.UserService.Domain.Entities;
using UserService.Application.DTOs;

namespace UserService.Application.Common.Interfaces.Services;

public interface IVNeIdSimulationService
{
    Task<SimulatedVNeIdResult> CompleteDriverSimulationAsync(SimulatedVNeIdRequest request, CancellationToken ct);
    Task<SimulatedVNeIdResult> CompleteOwnerSimulationAsync(SimulatedVNeIdRequest request, CancellationToken ct);
    Task<SimulatedVNeIdResult> CompleteOperatorSimulationAsync(Guid challengeId, bool consentConfirmed, string? consentVersion, CancellationToken ct);
    Task<VNeIdSimulationStatusDto> GetStatusByUserIdAsync(Guid userId, CancellationToken ct);
    Task<bool> HasCompletedSimulationAsync(Guid userId, CancellationToken ct);
}

