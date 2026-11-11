namespace SmartParking.OperationService.Domain.Entities;

public sealed partial class ReservationRow
{
    public Guid? RequestId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Guid? TermsId { get; private set; }
    public string? TermsVersion { get; private set; }
    public string? SelectedPaymentMethod { get; private set; }
    public decimal? BookedAmount { get; private set; }
    public decimal? DepositAmount { get; private set; }
    public string? Currency { get; private set; }
    public string? AcceptedPricingSnapshot { get; private set; }
    public Guid? PaymentOrderId { get; private set; }
    public Guid? CollectionId { get; private set; }
    public Guid? ConfirmationEffectId { get; private set; }
    public DateTimeOffset? ConfirmationEvaluatedAt { get; private set; }
    public DateTimeOffset? ClaimEndTime { get; private set; }

    public static ReservationRow Prepare(Guid id, Guid requestId, Guid accountId, Guid vehicleId,
        Guid tenantId, Guid siteId, Guid slotId, Guid termsId, string termsVersion, string method,
        DateTimeOffset start, DateTimeOffset end, DateTimeOffset claimEnd, DateTimeOffset now,
        DateTimeOffset holdExpiresAt, decimal booked, decimal deposit, string currency,
        string policies, string pricing)
    {
        if (now >= start || holdExpiresAt > start || holdExpiresAt <= now || end <= start || claimEnd < end ||
            booked < 0 || deposit < 0 || booked > 0 && deposit <= 0 || booked == 0 && deposit != 0)
            throw new ArgumentException("Invalid accepted booking terms.");
        return new ReservationRow
        {
            Id = id, RequestId = requestId, AccountId = accountId, VehicleId = vehicleId,
            TenantId = tenantId, SiteId = siteId, TargetSlotId = slotId, ActiveClaimSlotId = slotId,
            TermsId = termsId, TermsVersion = termsVersion, SelectedPaymentMethod = method,
            ExpectedStartTime = start, ExpectedEndTime = end, ClaimEndTime = claimEnd,
            HoldExpiresAt = holdExpiresAt, CreatedAt = now, BookedAmount = booked, DepositAmount = deposit,
            Currency = currency, AcceptedOperationalPolicySnapshot = policies, AcceptedPricingSnapshot = pricing,
            VehicleType = "CAR", Status = "PENDING_PAYMENT", Version = 1
        };
    }

    public void RecordOrder(Guid orderId)
    {
        if (orderId == Guid.Empty || PaymentOrderId is not null && PaymentOrderId != orderId)
            throw new InvalidOperationException("Order identity cannot change.");
        PaymentOrderId = orderId;
    }

    public void ReleaseExpiredHold(DateTimeOffset now)
    {
        if (Status == "PENDING_PAYMENT" && now >= HoldExpiresAt)
        { Status = "EXPIRED"; ActiveClaimSlotId = null; }
    }

    // One eligibility timestamp captured under the required guards. Commit may finish later.
    // Caller persists this transition, receipt, dependency outcome and outbox fact atomically.
    public void ConfirmAtEvaluation(Guid? collectionId, DateTimeOffset evaluatedAt)
    {
        if (Status is not ("PENDING_PAYMENT" or "EXPIRED") || evaluatedAt < CreatedAt ||
            evaluatedAt >= ExpectedStartTime || ConfirmationEvaluatedAt is not null ||
            collectionId == Guid.Empty || DepositAmount > 0 && collectionId is null ||
            DepositAmount == 0 && collectionId is not null)
            throw new InvalidOperationException("Confirmation is not eligible at this checkpoint.");
        CollectionId = collectionId;
        ConfirmationEffectId = Guid.NewGuid();
        ConfirmationEvaluatedAt = evaluatedAt;
        ConfirmedAt = evaluatedAt;
        ActiveClaimSlotId = TargetSlotId;
        Status = "CONFIRMED";
    }
}
