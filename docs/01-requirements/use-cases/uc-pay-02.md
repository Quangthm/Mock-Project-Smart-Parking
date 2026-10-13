# UC-PAY-02 — Inspect invoices, payment history and charge explanations

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, scoped staff

[Use-case index](README.md) · [SRS module](../srs/modules/06-pricing-payments.md)

## Goal

Inspect invoices, payment history and charge explanations.

## Preconditions

Current access to own or scoped financial facts.

## Main flow

1. Read verified payment/refund states and retained financial links.
2. Generate replay-safe server PDF invoice for settled obligation/adjustment.
3. Explain backend-calculated breakdown and attributable credit/refund history.

## Alternatives and failures

Cross-account access denied; pending payout is not shown complete; image retention does not inherit seven-year financial retention merely by attachment.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-PAY-07  
**US:** US-D06  
**Rules:** BR-PAY-03, BR-AUTH-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
