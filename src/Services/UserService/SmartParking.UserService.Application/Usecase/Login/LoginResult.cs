using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Login;

public sealed record LoginResult(UserSessionDto? Session, string? ErrorCode = null)
{
    public bool IsSuccess => Session is not null;
}
