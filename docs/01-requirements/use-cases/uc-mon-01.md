# UC-MON-01 — Configure and offer monthly/yearly plans

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner

[Use-case index](README.md) · [SRS module](../srs/modules/07-monthly-passes.md)

## Goal

Configure and offer monthly/yearly plans.

## Preconditions

Owned-lot plan configuration with required Payment terms.

## Main flow

1. Enable monthly and/or yearly Driver parking products separately from price; define available-only scope and full-period Payment terms.
2. Validate product-specific commercial configuration: yearly covers 12 calendar months and at most one period purchased in advance; monthly maximum remains three. Yearly policy fields/rates/defaults require Q05 completion; do not copy monthly defaults.
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
