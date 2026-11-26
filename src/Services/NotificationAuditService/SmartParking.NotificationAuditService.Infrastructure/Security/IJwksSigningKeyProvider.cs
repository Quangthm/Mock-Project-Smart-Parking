namespace SmartParking.NotificationAuditService.Infrastructure.Security;

public interface IJwksSigningKeyProvider
{
    bool IsConfigured { get; }
    IReadOnlyList<JwksRsaKey> ResolveKeys(string? kid);
}
