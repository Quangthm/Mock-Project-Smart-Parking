namespace UserService.Application.DTOs
{
    /// <summary>
    /// Login data transfer object.
    /// </summary>
    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
