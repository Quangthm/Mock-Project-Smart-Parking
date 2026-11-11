# UC-RES-03 — Cancel, expire or mark no-show; inspect reservation history

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, assigned Operator, Operations scheduler

[Use-case index](README.md) · [SRS module](../srs/modules/04-reservations-allocation.md)

## Goal

Cancel, expire or mark no-show; inspect reservation history.

## Preconditions

Existing reservation and current lifecycle/version.

## Authorization and pending-workflow boundary — approved 2026-11-04

Cancel own future reservation is REQUEST; own history/status reads remain TOKEN; expiry/no-show scheduler work uses SERVICE. These paths must not share an assumed customer actor or blanket freshness label. Admit a separate durable cancellation receipt and return 202. Before timely correlated Allowed permit read-only preparation only; no cancellation/release/refund effect. Denial/unavailability leaves the booking usable. Serialize the final cancellation with actual entry; one wins.

See the [operation matrix](../srs/authorization-operation-classification.md) and [mandatory REQUEST design checklist](../srs/authorization-operation-classification.md#5-implementation-documentation-checklist). Specify branch eligibility, correlation/attempts, pending observation, combined outcomes, deadlines, idempotency, revocation race, pivot, compensation and late/unknown handling before implementing the affected workflow. This amendment approves direction, not a completed Saga or wire contract.

## Main flow

1. Show own history and independent money/visit state.
2. After Allowed, accept eligible Driver cancellation once, release unused claims and calculate refund from eligible collected money. An earned facility-failure right survives recovery and is exercisable until accepted PARKING or unused closure; gate arrival alone does not end it. Earning eligibility creates no payout; no collected value means no payout.
3. Exclude facility-failure exercise/cancellation and unpaid expiry; count accepted voluntary cancellation once under global restriction policy; still allow existing cancellations while restricted.
4. Expire unpaid holds at H; pre-T compatible reacquisition remains possible. At/after T no unconfirmed booking may confirm. Late verified collection reconciles/refunds without entitlement. Confirmed but unfulfilled at T cancels for facility failure, with one applicable refund intent/notification.
5. For a usable booking, mark no-show only strictly after enabled arrival cutoff with no authoritative timely evidence; with tolerance disabled expire unused rights at E. Preserve history and earned rights until entry/closure.

## Alternatives and failures

Do not cancel PARKING as unused. Delayed authoritative timely-arrival evidence gets Operator correction and capacity recovery without displacement, or failure handling. No-show/cancellation creates no unpaid-remainder penalty.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-RES-06, FR-RES-08, FR-RES-09, FR-RES-10, FR-RES-12  
**US:** US-D01, US-D05, US-D10, US-O01, US-O05  
**Rules:** BR-RES-03, BR-RES-05, BR-RES-07

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
