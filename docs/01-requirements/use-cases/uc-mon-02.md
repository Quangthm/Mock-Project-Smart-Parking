# UC-MON-02 — Buy a monthly/yearly pass and activate verified rights

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operations, Payment

[Use-case index](README.md) Â· [SRS module](../srs/modules/07-monthly-passes.md)

## Goal

Buy a monthly/yearly pass and activate verified rights.

## Preconditions

Eligible bound account/vehicle/location and reviewed plan.

## Main flow

1. Select an explicitly enabled monthly/yearly product and reviewed product-specific terms. Monthly calculates immediate S=A or future local-midnight S with original anchor/[S,E); yearly covers 12 calendar months with boundary details specified under Q05.
2. Create monthly order using effective `MONTHLY_PAYMENT_HOLD_DURATION` (default 15 minutes) and effective `MONTHLY_MAX_PREPAID_TERMS` (default 3), or yearly order with at most one yearly period in advance and separately agreed deadline/policy. Neither holds parking capacity; annual months do not consume the monthly effective prepaid-term limit.
3. Verify full payment then commit entitlement before the product's accepted deadline; monthly strict A<H remains. Yearly deadline values are design-pending, not invented.
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
