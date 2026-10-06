using MediatR;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.Application.Usecase.Session;

public sealed record RefreshCommand(string? RefreshToken) : IRequest<UserSessionDto>;

public sealed class RefreshCommandHandler(IUnitOfWork unitOfWork, IAuthSessionStore sessions,
    AuthSessionService auth, TimeProvider clock) : IRequestHandler<RefreshCommand, UserSessionDto>
{
    public async Task<UserSessionDto> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new AuthException("MISSING_REFRESH_TOKEN", "Refresh token is required.", 400);
        var session = await sessions.FindByRefreshHashAsync(AuthSessionService.HashRefreshToken(request.RefreshToken), cancellationToken);
        if (session is null || session.IsRevoked || session.ExpiresAt <= clock.GetUtcNow())
            throw AuthException.InvalidToken();
        var user = await unitOfWork.UserRepository.GetByIdWithRolesAsync(session.UserId, cancellationToken);
        if (user is null || user.DeletedOn != null || user.Status != UserStatus.Active || AccessTokenService.CurrentRole(user) is null)
            throw AuthException.InvalidToken();
        return await auth.RotateAsync(user, session, cancellationToken);
    }
}
