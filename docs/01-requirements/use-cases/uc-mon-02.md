# UC-MON-02 — Buy a monthly pass and activate verified rights

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operations, Payment

[Use-case index](README.md) · [SRS module](../srs/modules/07-monthly-passes.md)

## Goal

Buy a monthly pass and activate verified rights.

## Preconditions

Eligible bound account/vehicle/location and reviewed plan.

## Main flow

1. Calculate immediate S=A or future local-midnight S with original calendar anchor and [S,E).
2. Create monthly order with 15-minute deadline, no parking-capacity hold, and max three consecutive prepaid periods.
3. Verify full payment then commit entitlement only A<H.
4. Show independent order/money/entitlement states and invoice; pass alone protects zero spaces.

## Alternatives and failures

Exactly H or later cannot activate even if provider paidAt was earlier; reconcile/refund money. Duplicate/unknown/mismatched money never silently grants rights.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-MON-02, FR-MON-03, FR-MON-04, FR-MON-05, FR-MON-06  
**US:** US-D13, US-O07, US-OW06  
**Rules:** M-01, M-02, M-03, M-04, M-07

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
