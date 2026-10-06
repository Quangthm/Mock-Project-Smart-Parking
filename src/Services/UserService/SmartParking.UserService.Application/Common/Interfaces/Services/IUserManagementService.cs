using UserService.Application.DTOs;

namespace UserService.Application.Common.Interfaces.Services;

public interface IUserManagementService
{
    Task<ManagedUsersPage> ListAsync(Guid adminId, UserSearchDto query, CancellationToken ct);
    Task<ManagedUserDetail> GetAsync(Guid adminId, Guid id, CancellationToken ct);
    Task<ManagedUserDto> SetStatusAsync(Guid adminId, Guid id, UserStatusDto body, CancellationToken ct);
}
