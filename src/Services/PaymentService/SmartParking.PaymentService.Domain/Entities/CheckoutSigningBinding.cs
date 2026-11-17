namespace SmartParking.PaymentService.Domain.Entities;

// Immutable financial verification metadata; independent of tariff history retention.
public sealed class CheckoutSigningBinding
{
    public long OrderCode { get; private set; }
    public Guid PaymentOrderId { get; private set; }
    public string ProtectedBinding { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string? EvidenceReference { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
