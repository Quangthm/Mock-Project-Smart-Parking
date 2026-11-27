# UC-RES-01 — Create a parking reservation

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Payment service

[Use-case index](README.md) · [SRS module](../srs/modules/04-reservations-allocation.md)

## Goal

Create a parking reservation.

## Preconditions

Authenticated Driver with selected or newly registered CAR and selected slot. The Driver accepts the authoritative versioned quote before submission.

## Authorization and pending-workflow boundary — approved 2026-11-04

Create reservation is REQUEST. Authenticate locally, atomically admit pending workflow plus outbox and return pending before final outcome. D1 selects read-only preparation before Allowed. The first reservation, capacity claim and payment-order intent require a current timely Allowed plus successful business checks; no provisional pre-ALLOW claim is permitted in this slice. The existing five-minute payment hold does not authorize a speculative authorization hold.

See the [operation matrix](../srs/authorization-operation-classification.md) and [mandatory REQUEST design checklist](../srs/authorization-operation-classification.md#5-implementation-documentation-checklist). Specify branch eligibility, correlation/attempts, pending observation, combined outcomes, deadlines, idempotency, revocation race, pivot, compensation and late/unknown handling before implementing the affected workflow. This amendment approves direction, not a completed Saga or wire contract.

## Main flow

1. Resolve slot site/tenant, check prospective plate coverage and obtain Payment’s versioned quote and disclosures. Driver accepts before any hold.
2. Durably admit the plate/slot/interval/method and accepted quote reference with a stable idempotency key. Return 202. Check Account-current, plate ownership and Facility eligibility concurrently. Show committed progress over SignalR with one recovery read per connection, reconnect or restoration.
3. Guard local policy, unchanged quote/coverage, same-plate/site overlap, physical sessions and capacity. Atomically commit the claim, PENDING_PAYMENT reservation, terminal admission and hold outbox. H=min(creation + effective hold, T), ordinary default 5 minutes.
4. Open checkout when the real order is ready. Require verified collection before guarded confirmation. Initial subscription eligibility returns false. Future covered bookings start PENDING_COMMERCIAL_ACCEPTANCE and require authoritative zero-amount acceptance with linked order and receipt evidence.
5. Show CONFIRMED only after guarded Operations commit; the single evaluation timestamp must be strictly before T. Issue booking details; applicable allocation follows its own lifecycle.

## Alternatives and failures

Positive deposit rounded to zero fails validation. Concurrent vehicle/capacity loser receives conflict; retry returns same result. After ordinary hold expiry, reacquire only if compatible and guarded confirmation is evaluated strictly before T; at/after T expire and reconcile/refund verified money without entitlement; no general pay-on-arrival bypass.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-01, FR-BAS-04, FR-CAP-03, FR-CAP-04, FR-POL-06, FR-RES-01, FR-RES-02, FR-RES-03, FR-RES-04, FR-RES-05  
**US:** US-D01, US-O01, US-O05, US-OW02  
**Rules:** BR-RES-01, BR-RES-02, BR-RES-06, BR-CAP-02, BR-CAP-03, BR-POL-04

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
