using MediatR;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Application.Usecase.Session;

public sealed record LogoutCommand(string SessionId) : IRequest;

public sealed class LogoutCommandHandler(IAuthSessionStore sessions) : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sessions.Revoke(request.SessionId);
        return Task.CompletedTask;
    }
}
