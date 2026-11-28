# UC-INC-02 — Appeal and handle a refund/escalation

**Status:** CONFIRMED — implementation pending / acceptance NOT RUN
**Actors:** Driver, assigned Operator, Owner on escalation

[Use-case index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md) · [28 November decisions](../../05-project-management/decision-log/2026-11-28-mvp-decisions-amendment.md#c-appeals-and-checkout)

## Goal

Dispute an eligible recorded obligation and follow the decision and any separate adjustment.

## Preconditions

Financial appeals require an authoritative existing reference and are limited to previously recorded paid parking charges, fines paid in completed checkout, and officially issued fines linked to ongoing sessions, including unpaid fines. General current unpaid parking fees, active sessions without an existing disputed fine, hypothetical or unconfirmed violations or fines, and facility-failure explanation requests are excluded. Existing Driver ownership and staff scope apply. Operations owns the issued fine identity and its association with the session and authoritative violation decision. Other / General support is a distinct asynchronous case category with optional financial context and no automatic refund authority.

## Main flow

1. The Driver submits the authoritative reference and permitted evidence.
2. The scoped Operator reviews SUBMITTED → IN_REVIEW and records ACCEPTED or REJECTED with reason. The Owner handles scoped escalation. Monthly cancellation retains its separate flow.
3. A pending appeal leaves the issued fine payable and normal checkout, payment and departure available under existing settlement and safety rules.
4. Operations durably freezes the obligation and fine amounts for the checkout attempt before requesting preparation from Payment. The cutoff and decision commit have a definitive ordering independent of the order ID or QR code.
5. Acceptance before the cutoff may correct or waive the unpaid fine for subsequent preparation. After the cutoff, preserve the prepared amounts and record the outstanding adjustment. Verified payment allows an eligible refund request. Definitive unpaid termination lets the correction apply to the next valid preparation.
6. Show a customer-safe appeal decision and independent adjustment or Payment outcome. Payment validates the refundable collected balance under BR-REF-01 and owns financial execution and verified refund outcomes.

## Alternatives and failures

While payment is pending or unknown, retain the accepted decision and outstanding adjustment. A timeout, expired QR, lost response or missing local order reference is insufficient to prove nonpayment. Reconcile late, unexpected or duplicate collection through Payment. Acceptance alone causes no change to the prepared amount, cancellation, replacement, or regeneration of a Payment order. Replay returns the existing outcome without duplicate effects. Monthly and yearly coverage applies to parking fees and leaves issued fines in force. The Driver reads an existing facility-failure reason through [UC-RES-03](uc-res-03.md) or a supported notification.

## Postconditions

Preserve the original violation, fine, payment, invoice and appeal decision history. ACCEPTED and REFUNDED are separate outcomes.

## Traceability

**FR:** FR-INC-05, FR-INC-07, FR-PAY-06
**US:** US-D07, US-D10, US-OW04, US-OW07
**Rules:** BR-VIOL-04, BR-REF-01

## Remaining work

See the [service-owner handoff](../../05-project-management/handoffs/2026-11-28-mvp-service-owner-handoff.md) for fine identity, checkout cutoff, attribution and integration gaps. My Parking Session and new ownership predicates remain deferred.
