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

public class InvoicePricingSnapshot
{
    public string OrderCode { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string CustomerType { get; set; } = "GUEST_DRIVER";
    public string? DisplayName { get; set; }
    public string? SiteName { get; set; }
    public string VehicleType { get; set; } = "CAR";
    public string? PlateNumber { get; set; }

    public DateTimeOffset EntryTime { get; set; }
    public DateTimeOffset PricingCutoffAt { get; set; }
    public int TotalDurationMinutes { get; set; }
    public int BillableDurationMinutes { get; set; }

    public Guid TariffId { get; set; }
    public int TariffVersion { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public int FreeParkingGraceMinutes { get; set; }

    public decimal SubtotalParkingFee { get; set; }
    public decimal TotalPenaltyFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalTotalAmount { get; set; }
    public string Currency { get; set; } = "VND";

    public string Status { get; set; } = "PENDING";
    public bool IsSettled { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaymentMethod { get; set; }
    public string? TransactionReference { get; set; }
    public string? GatewayTransactionId { get; set; }

    public List<InvoiceLineItem> LineItems { get; set; } = new();
}

public class InvoiceLineItem
{
    public string ItemType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public int CoveredMinutes { get; set; }
    public int ChargeableMinutes { get; set; }
    public decimal UnitRate { get; set; }
    public string Unit { get; set; } = "HOURLY_BLOCK";
    public decimal Amount { get; set; }
}
