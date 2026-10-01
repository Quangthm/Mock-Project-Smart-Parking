using MediatR;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Login;

public class LoginCommandHandler
    : IRequestHandler<LoginCommand, UserSessionDto>
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

    public async Task<UserSessionDto> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user = this.unitOfWork.UserRepository
            .Query()
            .FirstOrDefault(x =>
                x.Email == request.Email);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        // Add your status check here if your User entity has Status.

        if (user.PasswordHash != request.Password)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        var accessToken =
            this.accessTokenService
                .GenerateAccessToken(user);

        var refreshToken =
            Guid.NewGuid().ToString("N");

        return new UserSessionDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }
}

public class AccountLockedException : Exception
{
    public AccountLockedException(string message)
        : base(message)
    {
    }
}