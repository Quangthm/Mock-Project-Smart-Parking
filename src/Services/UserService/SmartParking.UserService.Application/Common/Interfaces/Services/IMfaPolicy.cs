using UserService.Application.DTOs;

namespace UserService.Application.Common.Interfaces.Services;
public interface IMfaPolicy
{
    Task<bool> MfaEnabledAsync(Guid userId, CancellationToken ct);
    Task<MfaLoginChallengeDto> CreateMfaLoginChallengeAsync(Guid userId, CancellationToken ct);
}
