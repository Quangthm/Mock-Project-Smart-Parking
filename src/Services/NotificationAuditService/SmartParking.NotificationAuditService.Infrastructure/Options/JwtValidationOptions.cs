namespace SmartParking.NotificationAuditService.Infrastructure.Options;

public sealed class JwtValidationOptions
{
    public const string SectionName = "JwtValidation";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string JwksUri { get; set; } = string.Empty;
    public int CacheDurationSeconds { get; set; } = 300;
    public int RequestTimeoutSeconds { get; set; } = 5;
    public int MinRefreshIntervalSeconds { get; set; } = 10;
}
