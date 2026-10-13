# UC-RES-02 — Allocate, queue and reallocate reservations

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operations scheduler, assigned Operator, Driver

[Use-case index](README.md) · [SRS module](../srs/modules/04-reservations-allocation.md)

## Goal

Allocate, queue and reallocate reservations.

## Preconditions

Confirmed entitlement; allocation/recovery time or accepted reallocation request.

## Main flow

1. Rank ordinary reservations by start then confirmation; choose requested/same-zone/fallback/nearest compatible resource.
2. Commit allocation revision and claim updates; preserve requested preference/history.
3. For unexpected loss after allocation, queue affected arrivals and feasible resources; preserve occupied/unaffected claims and use Operator for genuine ties.
4. Offer conforming alternative for 5 minutes; accept only before authoritative expiry.
5. Count up to two accepted Driver reallocation requests; system recovery/retries/invalid requests do not spend allowance.

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
