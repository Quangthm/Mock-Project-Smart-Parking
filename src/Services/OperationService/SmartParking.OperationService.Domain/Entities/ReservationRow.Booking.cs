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
    public string OrderProvisioningState { get; private set; } = "NOT_REQUIRED";
    public DateTimeOffset? OrderProvisioningNextActionAt { get; private set; }
    public int OrderProvisioningAttempts { get; private set; }
    public string? OrderProvisioningCode { get; private set; }
    public string ZeroPriceAcceptanceState { get; private set; } = "NOT_REQUIRED";
    public DateTimeOffset? ZeroPriceAcceptanceNextActionAt { get; private set; }
    public int ZeroPriceAcceptanceAttempts { get; private set; }
    public string? ZeroPriceAcceptanceCode { get; private set; }
    public string ConfirmationState { get; private set; } = "AWAITING_COLLECTION";
    public DateTimeOffset? ConfirmationNextActionAt { get; private set; }
    public string? ConfirmationCode { get; private set; }
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
            OrderProvisioningState = deposit == 0 ? "NOT_REQUIRED" : "PENDING",
            OrderProvisioningNextActionAt = deposit == 0 ? null : now,
            ZeroPriceAcceptanceState = deposit == 0 ? "PENDING" : "NOT_REQUIRED",
            ZeroPriceAcceptanceNextActionAt = deposit == 0 ? now : null,
            ConfirmationState = deposit == 0 ? "AWAITING_ZERO_PRICE_ACCEPTANCE" : "AWAITING_COLLECTION",
            VehicleType = "CAR", Status = "PENDING_PAYMENT", Version = 1
        };
    }

    public bool BeginOrderProvisioning(DateTimeOffset now, TimeSpan recoveryDelay)
    {
        if (PaymentOrderId is not null || DepositAmount is null or <= 0 ||
            OrderProvisioningState is not ("PENDING" or "UNKNOWN" or "IN_PROGRESS") ||
            OrderProvisioningNextActionAt is null || now < OrderProvisioningNextActionAt)
            return false;
        OrderProvisioningState = "IN_PROGRESS";
        OrderProvisioningAttempts = checked(OrderProvisioningAttempts + 1);
        OrderProvisioningNextActionAt = now.Add(recoveryDelay);
        return true;
    }

    public void DeferOrderProvisioning(DateTimeOffset nextActionAt, string state, string code)
    {
        if (PaymentOrderId is not null || state is not ("PENDING" or "UNKNOWN") ||
            OrderProvisioningState is not ("IN_PROGRESS" or "PENDING" or "UNKNOWN") ||
            string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Order provisioning cannot be deferred from its current state.");
        OrderProvisioningState = state;
        OrderProvisioningNextActionAt = nextActionAt;
        OrderProvisioningCode = code;
    }

    public void RecordOrderCreated(Guid orderId)
    {
        if (orderId == Guid.Empty || DepositAmount is null or <= 0 ||
            OrderProvisioningState is not ("IN_PROGRESS" or "PENDING" or "UNKNOWN" or "CREATED") ||
            PaymentOrderId is not null && PaymentOrderId != orderId)
            throw new InvalidOperationException("Order identity cannot change.");
        PaymentOrderId = orderId;
        OrderProvisioningState = "CREATED";
        OrderProvisioningNextActionAt = null;
        OrderProvisioningCode = null;
    }

    public void CloseOrderProvisioning(string code)
    {
        if (PaymentOrderId is not null || OrderProvisioningState is not ("IN_PROGRESS" or "PENDING" or "UNKNOWN") ||
            string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Order provisioning cannot be closed from its current state.");
        OrderProvisioningState = "CLOSED_NO_ORDER";
        OrderProvisioningNextActionAt = null;
        OrderProvisioningCode = code;
    }

    public void ReleaseExpiredHold(DateTimeOffset now)
    {
        if (Status == "PENDING_PAYMENT" && now >= HoldExpiresAt)
        { Status = "EXPIRED"; ActiveClaimSlotId = null; }
    }

    public void ReleaseHoldForOrderFailure(string code)
    {
        if (Status != "PENDING_PAYMENT" || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Only a pending reservation hold can be released for order failure.");
        Status = "EXPIRED";
        ActiveClaimSlotId = null;
        OrderProvisioningCode = code;
    }

    public void ReleaseHoldForUnavailableConfirmation(string code)
    {
        if (Status != "PENDING_PAYMENT" || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Only a pending reservation hold can be released for unavailable confirmation.");
        Status = "EXPIRED";
        ActiveClaimSlotId = null;
        ConfirmationCode = code;
    }

    public bool BeginZeroPriceAcceptance(DateTimeOffset now, TimeSpan recoveryDelay)
    {
        if (DepositAmount != 0 || ZeroPriceAcceptanceState is not ("PENDING" or "UNKNOWN" or "IN_PROGRESS") ||
            ZeroPriceAcceptanceNextActionAt is null || now < ZeroPriceAcceptanceNextActionAt)
            return false;
        ZeroPriceAcceptanceState = "IN_PROGRESS";
        ZeroPriceAcceptanceAttempts = checked(ZeroPriceAcceptanceAttempts + 1);
        ZeroPriceAcceptanceNextActionAt = now.Add(recoveryDelay);
        return true;
    }

    public bool BeginPaidConfirmation(Guid collectionId, DateTimeOffset nextActionAt)
    {
        if (collectionId == Guid.Empty || DepositAmount is null or <= 0 || Status is not ("PENDING_PAYMENT" or "EXPIRED") ||
            ConfirmationState is not ("AWAITING_COLLECTION" or "PENDING") ||
            ConfirmationEvaluatedAt is not null || nextActionAt < CreatedAt ||
            CollectionId is not null && CollectionId != collectionId)
            return false;
        CollectionId = collectionId;
        ConfirmationState = "PENDING";
        ConfirmationNextActionAt = nextActionAt;
        ConfirmationCode = null;
        return true;
    }

    public void DeferZeroPriceAcceptance(DateTimeOffset nextActionAt, string state, string code)
    {
        if (DepositAmount != 0 || state is not ("PENDING" or "UNKNOWN") ||
            ZeroPriceAcceptanceState is not ("IN_PROGRESS" or "PENDING" or "UNKNOWN") ||
            string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Zero-price acceptance cannot be deferred from its current state.");
        ZeroPriceAcceptanceState = state;
        ZeroPriceAcceptanceNextActionAt = nextActionAt;
        ZeroPriceAcceptanceCode = code;
    }

    public void AcceptZeroPriceTerms(DateTimeOffset now)
    {
        if (DepositAmount != 0 || ZeroPriceAcceptanceState is not ("IN_PROGRESS" or "UNKNOWN") ||
            ConfirmationState != "AWAITING_ZERO_PRICE_ACCEPTANCE")
            throw new InvalidOperationException("Trusted zero-price acceptance is required before confirmation.");
        ZeroPriceAcceptanceState = "ACCEPTED";
        ZeroPriceAcceptanceNextActionAt = null;
        ZeroPriceAcceptanceCode = null;
        ConfirmationState = "PENDING";
        ConfirmationNextActionAt = now;
    }

    public void RejectZeroPriceTerms(string code)
    {
        if (DepositAmount != 0 || ZeroPriceAcceptanceState is not ("IN_PROGRESS" or "PENDING" or "UNKNOWN") ||
            string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Zero-price terms cannot be rejected from their current state.");
        ZeroPriceAcceptanceState = "REJECTED";
        ZeroPriceAcceptanceNextActionAt = null;
        ZeroPriceAcceptanceCode = code;
        ConfirmationState = "NO_ENTITLEMENT";
        ConfirmationCode = code;
        ConfirmationNextActionAt = null;
        if (Status == "PENDING_PAYMENT")
        {
            Status = "EXPIRED";
            ActiveClaimSlotId = null;
        }
    }

    public bool DeferConfirmation(DateTimeOffset nextActionAt, string code)
    {
        if (ConfirmationState != "PENDING" || ConfirmationEvaluatedAt is not null)
            return false;
        ConfirmationNextActionAt = nextActionAt;
        ConfirmationCode = code;
        return true;
    }

    public void CloseConfirmation(string code)
    {
        if (ConfirmationState != "PENDING" || ConfirmationEvaluatedAt is not null ||
            string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Confirmation cannot be closed from its current state.");
        ConfirmationState = "NO_ENTITLEMENT";
        ConfirmationNextActionAt = null;
        ConfirmationCode = code;
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
        ConfirmationState = "CONFIRMED";
        ConfirmationNextActionAt = null;
        ConfirmationCode = null;
    }
}
