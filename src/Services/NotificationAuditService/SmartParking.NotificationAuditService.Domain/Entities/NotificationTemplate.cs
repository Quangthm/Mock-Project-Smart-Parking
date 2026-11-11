using SmartParking.NotificationAuditService.Domain.Common;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Domain.Entities;

public class NotificationTemplate : BaseEntity
{
    public string TemplateCode { get; private set; } = string.Empty;
    public int Version { get; private set; } = 1;
    public NotificationChannel Channel { get; private set; } = NotificationChannel.IN_APP;
    public string? Subject { get; private set; }
    public string BodyContent { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    // EF Core parameterless constructor
    protected NotificationTemplate() { }

    public static NotificationTemplate Create(
        string templateCode,
        NotificationChannel channel,
        string bodyContent,
        string? subject = null,
        int version = 1,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
            throw new NotificationDomainException("TemplateCode cannot be empty.");
        if (string.IsNullOrWhiteSpace(bodyContent))
            throw new NotificationDomainException("BodyContent cannot be empty.");
        if (version < 1)
            throw new NotificationDomainException("Version must be at least 1.");

        return new NotificationTemplate
        {
            TemplateCode = templateCode.Trim(),
            Channel = channel,
            Subject = subject?.Trim(),
            BodyContent = bodyContent.Trim(),
            Version = version,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateContent(string bodyContent, string? subject)
    {
        if (string.IsNullOrWhiteSpace(bodyContent))
            throw new NotificationDomainException("BodyContent cannot be empty.");

        BodyContent = bodyContent.Trim();
        Subject = subject?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
