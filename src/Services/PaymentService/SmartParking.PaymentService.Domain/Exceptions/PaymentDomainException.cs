namespace SmartParking.PaymentService.Domain.Exceptions;

public class PaymentDomainException : Exception
{
    public const string InvalidCutoffTime = "INVALID_CUTOFF_TIME";
    public const string ForbiddenInvoiceAccess = "FORBIDDEN_INVOICE_ACCESS";
    public const string OperatorSiteMismatch = "OPERATOR_SITE_MISMATCH";
    public const string CrossTenantAccessDenied = "CROSS_TENANT_ACCESS_DENIED";
    public const string InvoiceNotFound = "INVOICE_NOT_FOUND";
    public const string IdempotencyPayloadMismatch = "IDEMPOTENCY_PAYLOAD_MISMATCH";
    public const string UnsettledPaymentOrder = "UNSETTLED_PAYMENT_ORDER";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string OrderAlreadyPaid = "ORDER_ALREADY_PAID";
    public const string OrderExpired = "ORDER_EXPIRED";
    public const string TariffNotFound = "TARIFF_NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";

    public string Code { get; }
    public string ErrorCode => Code;
    public string? Field { get; }

    public PaymentDomainException(string code, string message, string? field = null)
        : base(message)
    {
        Code = code;
        Field = field;
    }
}
