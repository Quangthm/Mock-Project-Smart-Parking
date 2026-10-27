# UC-RES-02 — Allocate, queue and reallocate reservations

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operations scheduler, assigned Operator, Driver

[Use-case index](README.md) Â· [SRS module](../srs/modules/04-reservations-allocation.md)

## Goal

Allocate, queue and reallocate reservations.

## Preconditions

Confirmed entitlement; allocation/recovery time or accepted reallocation request.

## Main flow

1. Calculate `allocation_point = reservation_start - effective ALLOCATION_LEAD_TIME`; use the confirmed 1-hour default when no permitted override resolves. If a confirmed reservation is created/confirmed after its allocation point, evaluate it immediately with the same idempotent command as the scheduler.
2. Rank ordinary reservations by start then confirmation; choose requested/same-zone/fallback/nearest compatible resource.
3. Treat physical occupancy independently from reservation/protection projection. If the requested/protected resource is occupied by an unrelated current session, do not mark the future reservation fulfilled and do not displace the occupant.
4. Commit allocation revision and claim updates; preserve requested preference/history.
5. For unexpected loss or continuing unrelated occupation, queue affected arrivals and feasible resources; preserve occupied/unaffected claims and use Operator for genuine ties.
6. Offer a conforming alternative for the effective `ALTERNATIVE_OFFER_DURATION` (default 5 minutes); accept only before authoritative expiry.
7. Count accepted Driver reallocation requests against the effective `DRIVER_REALLOCATION_REQUEST_LIMIT` (default 2); system recovery/retries/invalid requests do not spend allowance.

### Clarifying examples

```text
Driver A reservation: 09:30–11:00
Driver B reservation: 10:00–12:00
effective ALLOCATION_LEAD_TIME: 30 minutes
Driver A allocation point: 09:00
Driver B allocation point: 09:30
```

The 30-minute value is illustrative only. A reservation confirmed after its point is evaluated immediately.

For Slot S-12, `OCCUPIED + PROTECTED` may mean Driver A is currently parked while Driver B's future reservation is protected. If Driver A departs, the slot may become `AVAILABLE + PROTECTED`. If Driver A remains when Driver B needs a usable allocation, use the normal compatible alternative/reallocation/conflict process. `OCCUPIED` never proves that Driver B is the occupant.


## Alternatives and failures

Offer expiry releases temporary offer claim only. Usable alternative refusal uses voluntary terms; no feasible conforming alternative uses failure handling. Accepted reallocation alone earns no refund.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-02, FR-BAS-03, FR-CAP-06, FR-RES-13  
**US:** US-D01, US-D17, US-O01, US-O02, US-O03, US-O05  
**Rules:** BR-RES-01, BR-RES-08, BR-CAP-04

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
