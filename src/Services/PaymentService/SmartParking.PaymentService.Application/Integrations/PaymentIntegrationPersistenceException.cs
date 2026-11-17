namespace SmartParking.PaymentService.Application.Integrations;

public sealed class PaymentIntegrationPersistenceException(string message, Exception innerException)
    : Exception(message, innerException)
{
    public const string ErrorCode = "PAYMENT_INTEGRATION_PERSISTENCE_FAILED";
    public string Code => ErrorCode;
}
