using MediatR;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.Application.Usecase.Session;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<UserInfoDto?>;

public sealed class GetCurrentUserQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetCurrentUserQuery, UserInfoDto?>
{
    public async Task<UserInfoDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await unitOfWork.UserRepository.GetByIdWithRolesAsync(request.UserId, cancellationToken);
        if (user is null || user.DeletedOn != null || user.Status != UserStatus.Active || AccessTokenService.CurrentRole(user) is null)
            return null;

        return new UserInfoDto
        {
            UserId = user.Id!.Value,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = AccessTokenService.CurrentRole(user)!
        };
    }
}
