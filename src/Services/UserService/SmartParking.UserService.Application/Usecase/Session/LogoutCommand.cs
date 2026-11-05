using MediatR;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Services;

namespace UserService.Application.Usecase.Session;

public sealed record LogoutCommand(string? RefreshToken) : IRequest;

public sealed class LogoutCommandHandler(IAuthSessionStore sessions, ICurrentAccessService access) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new AuthException("MISSING_REFRESH_TOKEN", "Refresh token is required.", 400);
        var current = await access.RequireAsync(cancellationToken);
        bool revoked;
        try
        {
            revoked = await sessions.RevokeAsync(current.Session.AccessTokenId, current.User.Id!.Value,
                AuthSessionService.HashRefreshToken(request.RefreshToken), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AuthException("REVOKE_SESSION_FAILED", "Could not revoke the session. Please try again.", 500, exception);
        }
        if (!revoked) throw AuthException.InvalidToken();
    }
}
