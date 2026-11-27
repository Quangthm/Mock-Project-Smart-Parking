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
    public Guid? QuoteId { get; private set; }
    public DateTimeOffset? QuoteValidUntil { get; private set; }
    public string? TariffVersion { get; private set; }
    public string? CoverageVersion { get; private set; }
    public bool? IsCovered { get; private set; }
    public decimal? CoveredAmount { get; private set; }
    public Guid? ZeroReceiptId { get; private set; }
    public Guid? PaymentOrderId { get; private set; }
    public string ConfirmationState { get; private set; } = "AWAITING_COLLECTION";
    public string? ConfirmationCode { get; private set; }
    public Guid? CollectionId { get; private set; }
    public string? CollectionFingerprint { get; private set; }
    public Guid? ConfirmationEffectId { get; private set; }
    public DateTimeOffset? ConfirmationEvaluatedAt { get; private set; }
    public DateTimeOffset? ClaimEndTime { get; private set; }

    public static ReservationRow PrepareQuote(Guid requestId, Guid accountId, string plate, Guid tenantId,
        Guid siteId, Guid slotId, string method, DateTimeOffset start, DateTimeOffset end, DateTimeOffset claimEnd,
        DateTimeOffset now, DateTimeOffset? holdExpiresAt, decimal booked, decimal covered, decimal deposit, string currency,
        string policies, Guid quoteId, DateTimeOffset validUntil, string tariffVersion, string coverageVersion, bool isCovered)
    {
        if (quoteId == Guid.Empty || now >= validUntil ||
            string.IsNullOrWhiteSpace(tariffVersion) || string.IsNullOrWhiteSpace(coverageVersion) ||
            Reservations.ReservationCarPlate.Canonicalize(plate) != plate || string.IsNullOrWhiteSpace(plate) ||
            currency != "VND" || covered != (isCovered ? booked : 0m) || covered < 0 || booked != covered + deposit)
            throw new ArgumentException("Invalid quote acceptance.");
        var row = PrepareHold(requestId, requestId, accountId, tenantId, siteId, slotId, method, start, end,
            claimEnd, now, holdExpiresAt, booked, deposit, currency, policies, covered);
        row.OriginalPlate = row.NormalizedPlate = plate;
        row.QuoteId = quoteId;
        row.QuoteValidUntil = validUntil;
        row.TariffVersion = tariffVersion;
        row.CoverageVersion = coverageVersion;
        row.CoveredAmount = covered;
        row.IsCovered = isCovered;
        return row;
    }

    private static ReservationRow PrepareHold(Guid id, Guid requestId, Guid accountId, Guid tenantId, Guid siteId,
        Guid slotId, string method, DateTimeOffset start, DateTimeOffset end, DateTimeOffset claimEnd, DateTimeOffset now,
        DateTimeOffset? holdExpiresAt, decimal booked, decimal deposit, string currency, string policies, decimal covered)
    {
        if ((deposit == 0) != (holdExpiresAt == null) || now >= start || holdExpiresAt > start || holdExpiresAt <= now || end <= start || claimEnd < end ||
            booked < 0 || deposit < 0 || booked > covered && deposit <= 0 || booked == 0 && deposit != 0)
            throw new ArgumentException("Invalid accepted booking terms.");
        return new ReservationRow
        {
            Id = id, RequestId = requestId, AccountId = accountId,
            TenantId = tenantId, SiteId = siteId, TargetSlotId = slotId, ActiveClaimSlotId = slotId,
            SelectedPaymentMethod = method,
            ExpectedStartTime = start, ExpectedEndTime = end, ClaimEndTime = claimEnd,
            HoldExpiresAt = holdExpiresAt, CreatedAt = now, BookedAmount = booked, DepositAmount = deposit,
            Currency = currency, AcceptedOperationalPolicySnapshot = policies,
            ConfirmationState = "AWAITING_COLLECTION",
            VehicleType = "CAR", Status = deposit == 0 ? "CONFIRMED" : "PENDING_PAYMENT", Version = 1
        };
    }

    public void ReleaseExpiredHold(DateTimeOffset now)
    {
        if (Status == "PENDING_PAYMENT" && now >= HoldExpiresAt)
        {
            Status = "EXPIRED";
            ActiveClaimSlotId = null;
        }
    }

    public void ConfirmZeroAdmission(DateTimeOffset at)
    {
        if (DepositAmount != 0 || QuoteId == null || HoldExpiresAt != null || Status != "CONFIRMED" ||
            PaymentOrderId != null || CollectionId != null || ZeroReceiptId != null)
            throw new InvalidOperationException("Invalid zero-deposit admission.");
        RecordConfirmation(at);
    }

    public bool LinkZeroReceipt(Guid orderId, Guid receiptId)
    {
        if (DepositAmount != 0 || HoldExpiresAt.HasValue || CollectionId.HasValue || !ConfirmationEffectId.HasValue ||
            ConfirmedAt is null || ConfirmedAt != ConfirmationEvaluatedAt || ConfirmedAt >= ExpectedStartTime ||
            orderId == Guid.Empty || receiptId == Guid.Empty)
            throw new InvalidOperationException("Zero receipt requires a confirmed zero-deposit booking.");
        if (PaymentOrderId == orderId && ZeroReceiptId == receiptId) return false;
        if (PaymentOrderId.HasValue || ZeroReceiptId.HasValue)
            throw new InvalidOperationException("Conflicting zero receipt identity.");
        PaymentOrderId = orderId;
        ZeroReceiptId = receiptId;
        return true;
    }

    public bool BindCollection(Guid orderId, Guid collectionId, string fingerprint)
    {
        if (DepositAmount is null or <= 0 || orderId == Guid.Empty || collectionId == Guid.Empty ||
            (PaymentOrderId.HasValue && PaymentOrderId != orderId))
            throw new InvalidOperationException("Invalid reservation settlement identity.");
        if (CollectionId == collectionId && PaymentOrderId == orderId && CollectionFingerprint == fingerprint) return false;
        if (CollectionId.HasValue || CollectionFingerprint != null)
            throw new InvalidOperationException("Conflicting reservation settlement.");
        PaymentOrderId = orderId;
        CollectionId = collectionId;
        CollectionFingerprint = fingerprint;
        return true;
    }

    public void ConfirmPaid(DateTimeOffset at)
    {
        if (DepositAmount is null or <= 0 || !CollectionId.HasValue || !PaymentOrderId.HasValue ||
            Status is not ("PENDING_PAYMENT" or "EXPIRED"))
            throw new InvalidOperationException("Invalid paid confirmation.");
        RecordConfirmation(at);
    }

    public void DenyPaidEntitlement(string reason)
    {
        if (!CollectionId.HasValue || ConfirmationEffectId.HasValue)
            throw new InvalidOperationException("Only an undecided collection can be denied.");
        if (Status is "PENDING_PAYMENT" or "EXPIRED") Status = reason == "RESERVATION_START_PASSED" ? "EXPIRED" : "INVALIDATED";
        ActiveClaimSlotId = null;
        ConfirmationState = "NO_ENTITLEMENT";
        ConfirmationCode = reason;
    }

    private void RecordConfirmation(DateTimeOffset at)
    {
        if (at < CreatedAt || at >= ExpectedStartTime || ConfirmationEffectId.HasValue || TargetSlotId is null)
            throw new InvalidOperationException("Invalid confirmation checkpoint.");
        ConfirmationEffectId = Guid.NewGuid();
        ConfirmationEvaluatedAt = ConfirmedAt = at;
        ActiveClaimSlotId = TargetSlotId;
        Status = ConfirmationState = "CONFIRMED";
    }
}
