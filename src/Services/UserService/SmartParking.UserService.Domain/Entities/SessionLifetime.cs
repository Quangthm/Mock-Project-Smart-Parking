namespace SmartParking.UserService.Domain.Entities;

public static class SessionLifetime
{
    public const int AccessSeconds = 15 * 60;
    public static readonly TimeSpan RefreshInactivity = TimeSpan.FromHours(24);
    public static bool CanRefresh(AuthSession session, DateTimeOffset now) =>
        !session.IsRevoked && session.ExpiresAt > now &&
        (session.LastRefreshedAt ?? session.CreatedAt) + RefreshInactivity > now;
}
