using MediatR;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.Application.Usecase.Login;

public class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAccessTokenService accessTokenService;

    public LoginCommandHandler(
        IUnitOfWork unitOfWork,
        IAccessTokenService accessTokenService)
    {
        this.unitOfWork = unitOfWork;
        this.accessTokenService = accessTokenService;
    }

    public async Task<LoginResult> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user = await this.unitOfWork.UserRepository
            .GetByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            return new LoginResult(null);
        }

        // Plain-text comparison is used only for the seeded, in-memory demo account.
        if (user.Status != UserStatus.Active || user.PasswordHash != request.Password
            || !user.UserRoles.Any(role => !role.IsDeleted))
        {
            return new LoginResult(null);
        }

        var accessToken =
            this.accessTokenService
                .GenerateAccessToken(user);

        return new LoginResult(new UserSessionDto
        {
            AccessToken = accessToken,
            // No fake refresh token: refresh persistence/rotation is outside this demo.
            RefreshToken = null,
            ExpiresIn = AccessTokenService.LifetimeSeconds,
            User = new UserInfoDto
            {
                UserId = user.Id!.Value,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = user.UserRoles.First(role => !role.IsDeleted).RoleCode.ToLowerInvariant()
            }
        });
    }
}

public class AccountLockedException : Exception
{
    public AccountLockedException(string message)
        : base(message)
    {
    }
}
