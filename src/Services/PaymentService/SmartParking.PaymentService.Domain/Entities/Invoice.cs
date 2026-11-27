using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PaymentOrderId { get; set; }
    public PaymentOrder? PaymentOrder { get; set; }

    public Guid SiteId { get; set; }
    public ReferenceType ReferenceType { get; set; } = ReferenceType.PARKING_SESSION;
    public Guid ReferenceId { get; set; }
    public Guid? SessionId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;
    public int TotalParkedMinutes { get; set; }
    public decimal BaseParkingFee { get; set; }
    public decimal OverstayPenaltyFee { get; set; }
    public decimal FinalAmount { get; set; }

    public InvoicePricingSnapshot PricingSnapshot { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
