using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Login;

// Invalid credentials are an expected outcome, not an exceptional failure.
public sealed record LoginResult(UserSessionDto? Session)
{
    public bool IsSuccess => Session is not null;
}
