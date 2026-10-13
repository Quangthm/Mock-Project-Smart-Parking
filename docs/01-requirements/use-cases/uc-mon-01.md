# UC-MON-01 — Configure and offer monthly plans

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner

[Use-case index](README.md) · [SRS module](../srs/modules/07-monthly-passes.md)

## Goal

Configure and offer monthly plans.

## Preconditions

Owned-lot plan configuration with required Payment terms.

## Main flow

1. Define available-only plan scope and full calendar-period price.
2. Validate required commercial config and approved locked defaults.
3. Publish versioned plan/financial terms for future purchases.
4. Preserve already-paid periods on edits/sales closure.

## Alternatives and failures

No guaranteed slot/capacity, sales cap, automatic extension, transfer or suspension capability.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-MON-01, FR-MON-12, FR-MON-13  
**US:** US-OW06  
**Rules:** M-01, M-05, M-06, M-09, BR-POL-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
