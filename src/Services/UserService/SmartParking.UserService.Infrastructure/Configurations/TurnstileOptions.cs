namespace UserService.Infrastructure.Configurations;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";
    public bool Enabled { get; set; } = true; // Mặc định true để tránh vô tình tắt (P1-04)
    public string SecretKey { get; set; } = string.Empty;
    public string[] ExpectedHostnames { get; set; } = [];
}
