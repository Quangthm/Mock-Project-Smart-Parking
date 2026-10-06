using MediatR;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Services;

namespace UserService.Application.Usecase.Session;

public sealed record LogoutCommand(Guid AccessTokenId, Guid UserId, string? RefreshToken) : IRequest;

public sealed class LogoutCommandHandler(IAuthSessionStore sessions) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new AuthException("MISSING_REFRESH_TOKEN", "Refresh token is required.", 400);
        bool revoked;
        try
        {
            revoked = await sessions.RevokeAsync(request.AccessTokenId, request.UserId,
                AuthSessionService.HashRefreshToken(request.RefreshToken), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AuthException("REVOKE_SESSION_FAILED", "Could not revoke the session. Please try again.", 500, exception);
        }
        if (!revoked) throw AuthException.InvalidToken();
    }
}
