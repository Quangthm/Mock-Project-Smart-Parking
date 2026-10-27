using UserService.Application.DTOs;

namespace UserService.Application.Common.Interfaces.Services;

public interface IOwnerRegistrationService
{
    Task<OwnerApplicationDto> RegisterAsync(OwnerRegistrationDto body, CancellationToken ct);
    Task<IReadOnlyList<OwnerApplicationDto>> ListAsync(Guid adminId, CancellationToken ct);
    Task<OwnerApplicationDto> ReviewAsync(Guid adminId, Guid id, ReviewOwnerDto body, CancellationToken ct);
}
