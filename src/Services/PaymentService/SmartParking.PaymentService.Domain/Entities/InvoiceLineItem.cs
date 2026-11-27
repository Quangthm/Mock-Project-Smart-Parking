namespace SmartParking.PaymentService.Domain.Entities;

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
