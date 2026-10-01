namespace UserService.Application.DTOs
{
    /// <summary>
    /// User session data transfer object.
    /// </summary>
    public class UserSessionDto
    {
        public string? AccessToken { get; set; }

        public string? RefreshToken { get; set; }

        public string TokenType { get; set; } = "Bearer";

        public int ExpiresIn { get; set; }

        public UserInfoDto? User { get; set; }
    }

    public class UserInfoDto
    {
        public Guid UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}
