# UC-MON-05 — Cancel a monthly/yearly plan and reconcile refund

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Owner, Operations, Payment

[Use-case index](README.md) · [SRS module](../srs/modules/07-monthly-passes.md)

## Goal

Cancel a monthly/yearly plan and reconcile refund.

## Preconditions

Voluntary request or separately verified facility failure.

## Main flow

1. For voluntary request list all affected future reservations priced using coverage being cancelled.
2. Require Driver explicitly cancel dependent reservations through ordinary cancellation; no automatic cancellation/repricing/charge.
3. Recheck dependencies and accepted entitlement version at cancellation commit; Owner handles approval and C is accepted backend cancellation time.
4. For monthly, calculate per-period prestart100% or midperiod50% unused value. For yearly use separately accepted configurable refund terms; no yearly rate is approved here. Sum/round once and cap collected balance; execute refund separately.
5. For temporary closure retain pass/end date and refund verified unavailable interval once; permanent closure cancels remaining coverage with failure100% and overlap deduction.

## Alternatives and failures

A concurrent dependent reservation either commits first and blocks cancellation, or sees cancelled coverage and requires new pricing review. Driver may separately rebook at ordinary price with no capacity guarantee. Failure handling does not require A-01 voluntary prerequisite; occupancy never clears from refund.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-MON-11, FR-MON-14  
**US:** US-D15  
**Rules:** M-05, M-06, M-07, M-09

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
