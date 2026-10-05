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

        var firstAccount = user.Accounts.FirstOrDefault(a => a.Status == "ACTIVE");
        var firstRole = firstAccount?.AccountRoles.FirstOrDefault()?.RoleCode ?? "driver";

        if (user.Status != UserStatus.Active || user.PasswordHash != request.Password || firstAccount == null)
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
                Role = firstRole.ToLowerInvariant()
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
