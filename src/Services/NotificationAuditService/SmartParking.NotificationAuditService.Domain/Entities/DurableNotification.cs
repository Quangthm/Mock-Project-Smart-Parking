using SmartParking.NotificationAuditService.Domain.Common;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Domain.Entities;

public class DurableNotification : BaseEntity
{
    public string OriginatingEvent { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid RecipientId { get; private set; }
    public NotificationChannel Channel { get; private set; } = NotificationChannel.IN_APP;
    public string TemplateCode { get; private set; } = string.Empty;
    public string Payload { get; private set; } = "{}";
    public NotificationStatus Status { get; private set; } = NotificationStatus.PENDING;
    public int Attempts { get; private set; } = 0;
    public int MaxAttempts { get; private set; } = 3;
    public string? LastError { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; private set; }

    // EF Core parameterless constructor
    protected DurableNotification() { }

    public static DurableNotification Create(
        string originatingEvent,
        string idempotencyKey,
        Guid recipientId,
        NotificationChannel channel,
        string templateCode,
        string payload,
        int maxAttempts = 3,
        DateTimeOffset? scheduledAt = null)
    {
        if (string.IsNullOrWhiteSpace(originatingEvent))
            throw new NotificationDomainException("OriginatingEvent cannot be empty.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new NotificationDomainException("IdempotencyKey cannot be empty.");
        if (recipientId == Guid.Empty)
            throw new NotificationDomainException("RecipientId cannot be empty.");
        if (string.IsNullOrWhiteSpace(templateCode))
            throw new NotificationDomainException("TemplateCode cannot be empty.");
        if (maxAttempts < 1)
            throw new NotificationDomainException("MaxAttempts must be at least 1.");

        return new DurableNotification
        {
            OriginatingEvent = originatingEvent.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            RecipientId = recipientId,
            Channel = channel,
            TemplateCode = templateCode.Trim(),
            Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload.Trim(),
            Status = NotificationStatus.PENDING,
            Attempts = 0,
            MaxAttempts = maxAttempts,
            ScheduledAt = scheduledAt ?? DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkProcessing()
    {
        if (Status == NotificationStatus.DELIVERED)
            throw new NotificationDomainException("Cannot process a notification that has already been delivered.");

        Status = NotificationStatus.PROCESSING;
        Attempts++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDelivered()
    {
        Status = NotificationStatus.DELIVERED;
        DeliveredAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public void RecordFailure(string error)
    {
        LastError = error;
        UpdatedAt = DateTimeOffset.UtcNow;

        if (Attempts >= MaxAttempts)
        {
            Status = NotificationStatus.DEAD_LETTER;
        }
        else
        {
            Status = NotificationStatus.FAILED;
        }
    }

    public void ResetForRetry(DateTimeOffset retryAt)
    {
        if (Status == NotificationStatus.DELIVERED)
            throw new NotificationDomainException("Cannot retry an already delivered notification.");

        Status = NotificationStatus.PENDING;
        ScheduledAt = retryAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
