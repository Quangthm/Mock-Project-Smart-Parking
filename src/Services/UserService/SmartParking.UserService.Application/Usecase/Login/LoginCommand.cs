using MediatR;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Login
{
    /// <summary>
    /// Login command
    /// </summary>
    /// <seealso cref="MediatR.IRequest&lt;UserService.Application.DTOs.UserSessionDto&gt;" />
    public class LoginCommand : IRequest<UserSessionDto>
    {
        public string Email { get; set; }
        public string Password { get; set; }

    }
}
