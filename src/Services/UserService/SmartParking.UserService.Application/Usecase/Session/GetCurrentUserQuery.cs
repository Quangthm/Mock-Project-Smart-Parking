using MediatR;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Session;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<UserInfoDto?>;

public sealed class GetCurrentUserQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetCurrentUserQuery, UserInfoDto?>
{
    public Task<UserInfoDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = unitOfWork.UserRepository.Query().FirstOrDefault(user =>
            user.Id == request.UserId && user.DeletedOn == null && user.Status == UserStatus.Active);
        return Task.FromResult(user is null ? null : new UserInfoDto
        {
            UserId = user.Id!.Value,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = user.Accounts.FirstOrDefault(a => a.Status == "ACTIVE")?.AccountRoles.FirstOrDefault()?.RoleCode.ToLowerInvariant() ?? string.Empty
        });
    }
}
