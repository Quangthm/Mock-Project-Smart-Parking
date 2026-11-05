using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Application.DTOs.Notifications;

public record NotificationDto
{
    public Guid Id { get; init; }
    public string OriginatingEvent { get; init; } = string.Empty;
    public Guid RecipientId { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string TemplateCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Payload { get; init; } = "{}";
    public bool IsRead { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public int Attempts { get; init; }
    public int MaxAttempts { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset ScheduledAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public static NotificationDto FromEntity(DurableNotification entity)
    {
        return new NotificationDto
        {
            Id = entity.Id,
            OriginatingEvent = entity.OriginatingEvent,
            RecipientId = entity.RecipientId,
            Channel = entity.Channel.ToString(),
            TemplateCode = entity.TemplateCode,
            Title = entity.Title,
            Message = entity.Message,
            Payload = entity.Payload,
            IsRead = entity.IsRead,
            ReadAt = entity.ReadAt,
            Status = entity.Status.ToString(),
            Attempts = entity.Attempts,
            MaxAttempts = entity.MaxAttempts,
            LastError = entity.LastError,
            ScheduledAt = entity.ScheduledAt,
            DeliveredAt = entity.DeliveredAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
