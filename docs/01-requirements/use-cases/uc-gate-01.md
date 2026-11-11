# UC-GATE-01 — Check in a reserved, walk-in or monthly vehicle

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Assigned Operator, Driver

[Use-case index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md)

## Goal

Check in a reserved, walk-in or monthly vehicle.

## Preconditions

Gate arrival/entry requested; current operational scope.

## Authorization and pending-workflow boundary — approved 2026-11-04

User-initiated check-in/admission with an authenticated actor is REQUEST. Device/service paths use SERVICE; a separately authenticated operator path must declare its real actor and policy. Never fabricate a customer JWT for account-free walk-in. Only approved non-physical preparation may overlap authorization; barrier opening remains gated unless a separately approved safety/emergency rule applies.

See the [operation matrix](../srs/authorization-operation-classification.md) and [mandatory REQUEST design checklist](../srs/authorization-operation-classification.md#5-implementation-documentation-checklist). Specify branch eligibility, correlation/attempts, pending observation, combined outcomes, deadlines, idempotency, revocation race, pivot, compensation and late/unknown handling before implementing the affected workflow. This amendment approves direction, not a completed Saga or wire contract.

## Main flow

1. Verify arrival/identity and ticket/QR/manual evidence separately from actual entry.
2. Validate reservation/monthly rights or account-free walk-in and actual compatible space.
3. Authorize entry only when physically feasible; early entry uses disclosed extra terms.
4. Record actual entry/session and occupancy once; link claim without double deduction.

## Alternatives and failures

Scan/open alone creates no occupied session. Duplicate session, mismatch or unknown physical resource blocks automation; uncertain LPR uses manual verification. Monthly pass never guarantees full-lot admission.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-CAP-05, FR-GATE-01, FR-GATE-02, FR-GATE-03, FR-GATE-09, FR-RES-07, FR-RES-11  
**US:** US-D01, US-O01, US-O02, US-O08  
**Rules:** BR-GATE-01, BR-GATE-02, BR-RES-04, BR-CAP-05, M-04

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.

## Reservation first-slice boundary

See [D1–D4c](../../05-project-management/decision-log/2026-11-05-reservation-first-slice.md) and the [owner interface contract](../../03-design/api/reservation-first-slice-api-design.md#cross-owner-interfaces-to-agree). Reservation payment deadline H is capped by start T; confirmation eligibility must be evaluated strictly before T under the required guards; expose the result only after durable commit (10 November amendment), and later verified collections reconcile/refund without entitlement. Earned facility-failure eligibility survives recovery; payout is separate and limited to eligible collected value. Arrival alone leaves the right exercisable; accepted actual entry ends it. Commercial checkout/payment alone does not release occupancy; verified physical departure does. Preserve unrelated session/payment rules. Producer integration and deployment evidence are pending.
