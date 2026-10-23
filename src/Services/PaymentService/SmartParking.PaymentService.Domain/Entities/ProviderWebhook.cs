using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Domain.Entities;

public class ProviderWebhook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public WebhookProcessingState ProcessingState { get; set; } = WebhookProcessingState.PENDING;
    public string? Payload { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    public void MarkProcessed()
    {
        ProcessingState = WebhookProcessingState.PROCESSED;
    }

    public void MarkFailed()
    {
        ProcessingState = WebhookProcessingState.FAILED;
    }
}
