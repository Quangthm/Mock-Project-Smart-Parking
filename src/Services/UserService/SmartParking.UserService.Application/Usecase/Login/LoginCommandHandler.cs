using MediatR;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;

namespace UserService.Application.Usecase.Login;

public sealed class LoginCommandHandler(IUnitOfWork unitOfWork, IPasswordService passwords,
    AuthSessionService sessions, AuthenticationPolicy policy, TimeProvider clock)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = await unitOfWork.UserRepository.GetByEmailForLoginAsync(request.Email.Trim(), cancellationToken);
        if (user is null || user.DeletedOn != null)
            return new(null, "AUTH_FAILED");
        var now = clock.GetUtcNow();
        if (user.Status == UserStatus.Locked)
        {
            // Null means an administrative lock; timed locks expire automatically.
            if (user.LockedUntil is null || user.LockedUntil > now)
                return new(null, "ACCOUNT_LOCKED");
            user.Status = UserStatus.Active;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
        }
        if (user.Status != UserStatus.Active || AccessTokenService.CurrentRole(user) is null)
            return new(null, "AUTH_FAILED");
        if (!passwords.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= policy.MaxFailedLoginAttempts)
            {
                user.Status = UserStatus.Locked;
                user.LockedUntil = now.AddMinutes(policy.LockoutMinutes);
            }
            user.ModifiedOn = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(null, user.Status == UserStatus.Locked ? "ACCOUNT_LOCKED" : "AUTH_FAILED");
        }
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.ModifiedOn = now;
        var session = await sessions.CreateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(session);
    }
}
