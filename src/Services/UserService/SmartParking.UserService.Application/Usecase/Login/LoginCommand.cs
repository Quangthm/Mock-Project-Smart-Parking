using MediatR;

namespace UserService.Application.Usecase.Login
{
    /// <summary>
    /// Login command
    /// </summary>
    /// <seealso cref="MediatR.IRequest&lt;LoginResult&gt;" />
    public class LoginCommand : IRequest<LoginResult>
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

    }
}
