namespace SmartParking.PaymentService.Domain.Entities;

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
    public decimal DepositAmount { get; set; }
    public decimal DepositDeducted { get; set; }
    public decimal SurplusRefund { get; set; }
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
