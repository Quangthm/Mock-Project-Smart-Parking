namespace SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

public record ProcessPayOsWebhookResult(
    bool Success,
    string Message,
    string? ErrorCode = null,
    bool Retryable = false)
{
    internal Guid? RecordedTransactionId { get; init; }
    internal bool SettledOrder { get; init; }
}
