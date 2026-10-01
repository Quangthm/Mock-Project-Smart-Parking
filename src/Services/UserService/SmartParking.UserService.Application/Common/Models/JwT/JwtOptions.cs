namespace UserService.Application.Common.Models.JwT
{
    /// <summary>
    /// Jwt Options
    /// </summary>
    public class JwtOptions
    {
        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the issuer.
        /// </summary>
        /// <value>
        /// The issuer.
        /// </value>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the audience.
        /// </summary>
        /// <value>
        /// The audience.
        /// </value>
        public string Audience { get; set; } = string.Empty;
    }
}
