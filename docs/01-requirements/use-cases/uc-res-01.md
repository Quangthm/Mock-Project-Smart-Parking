# UC-RES-01 — Create a parking reservation

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Payment service

[Use-case index](README.md) · [SRS module](../srs/modules/04-reservations-allocation.md)

## Goal

Create a parking reservation.

## Preconditions

Authenticated eligible Driver with vehicle and reviewed terms.

## Authorization and pending-workflow boundary — approved 2026-11-04

Create reservation is REQUEST. Authenticate locally, atomically admit pending workflow plus outbox and return pending before final outcome. The business flow below requires accepted Account-current Allowed at final confirmation; any provisional capacity claim before Allowed requires separate approval. The existing five-minute payment hold does not authorize a speculative authorization hold.

See the [operation matrix](../srs/authorization-operation-classification.md) and [mandatory REQUEST design checklist](../srs/authorization-operation-classification.md#5-implementation-documentation-checklist). Specify branch eligibility, correlation/attempts, pending observation, combined outcomes, deadlines, idempotency, revocation race, pivot, compensation and late/unknown handling before implementing the affected workflow. This amendment approves direction, not a completed Saga or wire contract.

## Main flow

1. Validate 15-minute minimum, 24-hour maximum, 7-day horizon, same-vehicle compatibility, restrictions and optional lot cap.
2. Apply valid monthly coverage to minimum; display extras, prepayment and cancellation terms.
3. Atomically create compatible claim plus reservation: positive price PENDING_PAYMENT with 5-minute hold; genuine zero price CONFIRMED without monetary transaction.
4. For positive price collect FULL/PERCENTAGE/FIXED accepted amount and confirm on verified outcome; convert claim once.
5. Issue booking/ticket details; allocate immediately if already within one-hour allocation lead.

## Alternatives and failures

Positive deposit rounded to zero fails validation. Concurrent cap/capacity loser receives conflict; retry returns same result. After ordinary hold expiry reacquire or reconcile/refund; no general pay-on-arrival bypass.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-01, FR-BAS-04, FR-CAP-03, FR-CAP-04, FR-POL-06, FR-RES-01, FR-RES-02, FR-RES-03, FR-RES-04, FR-RES-05  
**US:** US-D01, US-O01, US-O05, US-OW02  
**Rules:** BR-RES-01, BR-RES-02, BR-RES-06, BR-CAP-02, BR-CAP-03, BR-POL-04

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
