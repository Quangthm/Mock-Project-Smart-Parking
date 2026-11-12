using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Domain.Entities;

public class ReservationQuote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? RequestedSlotId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    public decimal BookedAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public PrepaymentMode PrepaymentMode { get; set; } = PrepaymentMode.PERCENTAGE;
    public string Currency { get; set; } = "VND";

    public Guid? TariffId { get; set; }
    public Guid? TariffRevisionId { get; set; }
    public string TermsVersion { get; set; } = "v1";
    public string? CoverageVersion { get; set; }

    public DateTimeOffset ValidUntil { get; set; }
    public ReservationQuoteStatus Status { get; set; } = ReservationQuoteStatus.ISSUED;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsExpired(DateTimeOffset now) => now >= ValidUntil || Status == ReservationQuoteStatus.EXPIRED;

    public void MarkAccepted()
    {
        if (Status != ReservationQuoteStatus.ISSUED)
        {
            throw new InvalidOperationException($"Cannot accept quote in {Status} status.");
        }
        Status = ReservationQuoteStatus.ACCEPTED;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkExpired()
    {
        if (Status == ReservationQuoteStatus.ACCEPTED)
        {
            throw new InvalidOperationException("Cannot expire an already accepted quote.");
        }
        Status = ReservationQuoteStatus.EXPIRED;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
