using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Services
{
    /// <summary>
    /// Interface Jwt Services.
    /// </summary>
    public interface IAccessTokenService
    {
        /// <summary>
        /// Generates the access token.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns>Access Token</returns>
        public string GenerateAccessToken(User user);

        /// <summary>
        /// Validates the access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>Access token is valid or not.</returns>
        public bool ValidateAccessToken(string accessToken);


        /// <summary>
        /// Refreshes the access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>New access token.</returns>
        public string RefreshAccessToken(string accessToken, string refreshToken);
    }
}
