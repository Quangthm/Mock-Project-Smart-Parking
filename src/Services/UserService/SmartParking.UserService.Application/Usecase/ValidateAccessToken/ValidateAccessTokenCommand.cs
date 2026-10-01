using MediatR;

namespace UserService.Application.Usecase.ValidateAccessToken
{
    /// <summary>
    /// Validate access token command.
    /// </summary>
    public class ValidateAccessTokenCommand : IRequest<bool>
    {
        /// <summary>
        /// Gets or sets the access token.
        /// </summary>
        /// <value>
        /// The access token.
        /// </value>
        public string AccessToken { get; set; } = string.Empty;
    }
}
