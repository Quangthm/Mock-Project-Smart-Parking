using MediatR;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

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
        if (user.Status != UserStatus.Active || user.PasswordHash != request.Password)
        {
            return new LoginResult(null);
        }

        var accessToken =
            this.accessTokenService
                .GenerateAccessToken(user);

        var refreshToken =
            Guid.NewGuid().ToString("N");

        return new LoginResult(new UserSessionDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 300,
            User = new UserInfoDto
            {
                UserId = user.Id!.Value,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty
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
