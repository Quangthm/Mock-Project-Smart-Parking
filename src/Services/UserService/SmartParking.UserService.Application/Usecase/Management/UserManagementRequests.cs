using MediatR;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Management;

public sealed record ListUsersQuery(UserSearchDto Filter) : IRequest<ManagedUsersPage>;
public sealed record GetManagedUserQuery(Guid UserId) : IRequest<ManagedUserDetail>;
public sealed record SetManagedUserStatusCommand(Guid UserId, UserStatusDto Body) : IRequest<ManagedUserDto>;

public sealed class UserManagementRequestHandler(IUserManagementService users, ICurrentAccessService current,
    IAccountAuthorizationService authorization)
    : IRequestHandler<ListUsersQuery, ManagedUsersPage>, IRequestHandler<GetManagedUserQuery, ManagedUserDetail>,
      IRequestHandler<SetManagedUserStatusCommand, ManagedUserDto>
{
    private async Task<Guid> RequireAdminAsync(CancellationToken ct)
    {
        var id = (await current.RequireAsync(ct)).User.Id!.Value;
        await authorization.RequireAdminAsync(id, false, ct);
        return id;
    }
    public async Task<ManagedUsersPage> Handle(ListUsersQuery request, CancellationToken ct) =>
        await users.ListAsync(await RequireAdminAsync(ct), request.Filter, ct);
    public async Task<ManagedUserDetail> Handle(GetManagedUserQuery request, CancellationToken ct) =>
        await users.GetAsync(await RequireAdminAsync(ct), request.UserId, ct);
    public async Task<ManagedUserDto> Handle(SetManagedUserStatusCommand request, CancellationToken ct) =>
        await users.SetStatusAsync(await RequireAdminAsync(ct), request.UserId, request.Body, ct);
}
