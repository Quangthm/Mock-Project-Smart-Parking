using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.UserService.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;

namespace UserService.Application.Services
{
    /// <summary>
    /// Jwt Services.
    /// </summary>
    /// <seealso cref="UserService.Application.Common.Interfaces.Services.IAccessTokenService" />
    public class AccessTokenService : IAccessTokenService
    {
        /// <summary>
        /// The configuration
        /// </summary>
        private readonly IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessTokenService" /> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public AccessTokenService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Generates the access token.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns>
        /// Access Token
        /// </returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public string GenerateAccessToken(User user)
        {
            var jwtOptions = this.configuration.Get<JwtOptions>();

            ArgumentNullException.ThrowIfNull(jwtOptions);

            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("!B&8kG2p$3nZqQ6vLwXyTzS1dEr4Ui7h"));
            var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.Sub, user?.Email ?? "Test"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                // More information, More claims.
            };

            var tokenOptions = new JwtSecurityToken(issuer: jwtOptions.Issuer  ??string.Empty,
                                                    audience: jwtOptions.Audience ?? string.Empty,
                                                    claims: claims,
                                                    expires: DateTime.Now.AddMinutes(5),
                                                    signingCredentials: signinCredentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenOptions);

            return tokenString;
        }

        /// <summary>
        /// Validates the access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>
        /// Access token is valid or not.
        /// </returns>
        public bool ValidateAccessToken(string accessToken)
        {
            return true;
        }

        /// <summary>
        /// Refreshes the access token.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>
        /// New access token.
        /// </returns>
        public string RefreshAccessToken(string accessToken, string refreshToken)
        {
            var newAccessToken = string.Empty;

            return newAccessToken;
        }
    }
}
