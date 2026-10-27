# UC-INC-02 — Appeal and handle a refund/escalation

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, assigned Operator, Owner

[Use-case index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md)

## Goal

Appeal and handle a refund/escalation.

## Preconditions

Own eligible case; staff currently scoped to case.

## Main flow

1. Driver submits appeal and permitted evidence.
2. Operator reviews SUBMITTED → IN_REVIEW and accepts/rejects with reason.
3. Owner handles escalation; monthly cancellation follows its separate flow.
4. Payment reserves eligible refundable balance and executes approved adjustment; show redacted case and money states separately.

## Alternatives and failures

No monetary escalation threshold. Accepted appeal is not completed payout. Pending/UNKNOWN refund reserves balance; failure alone does not erase evidence or money.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-INC-05, FR-INC-07, FR-PAY-06  
**US:** US-D07, US-D10, US-OW04, US-OW07  
**Rules:** BR-VIOL-04, BR-REF-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
