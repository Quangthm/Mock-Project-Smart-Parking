namespace UserService.Application.Common.Models.JwT;

public sealed class AuthenticationPolicy
{
    public int MaxFailedLoginAttempts { get; set; } = 3;
    public int LockoutMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
