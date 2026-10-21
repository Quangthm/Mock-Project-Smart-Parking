using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Domain.Entities;

public class PaymentOrder
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? AccountId { get; set; }

    public string OrderCode { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public OrderType OrderType { get; set; } = OrderType.PARKING_FEE;
    public ReferenceType ReferenceType { get; set; } = ReferenceType.PARKING_SESSION;
    public Guid ReferenceId { get; set; }

    public decimal Amount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DepositDeducted { get; set; }
    public decimal FinalAmount { get; set; }

    public PaymentOrderStatus Status { get; set; } = PaymentOrderStatus.PENDING;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();

    public bool IsExpired(DateTimeOffset now) => ExpiresAt.HasValue && ExpiresAt.Value < now;

    public void MarkPaid(DateTimeOffset now)
    {
        Status = PaymentOrderStatus.PAID;
        UpdatedAt = now;
    }

    public void MarkCancelled(DateTimeOffset now)
    {
        Status = PaymentOrderStatus.CANCELLED;
        UpdatedAt = now;
    }

    public void MarkFailed(DateTimeOffset now)
    {
        Status = PaymentOrderStatus.FAILED;
        UpdatedAt = now;
    }
}
