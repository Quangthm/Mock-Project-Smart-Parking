# UC-LOT-01 — Manage facility profile, layout and service state

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, assigned Operator

[Use-case index](README.md) · [SRS module](../srs/modules/02-facility-capacity.md)

## Goal

Manage facility profile, layout and service state.

## Preconditions

Actor authorized for owned configuration or assigned operational task.

## Main flow

1. Configure profile/layout/category pools and supported 3D template with stable identifiers.
2. Submit planned service/capacity change; Operations restricts conflicting claims and evaluates existing commitments.
3. Commit recoverably after compatible recovery or explicit failure outcomes.
4. For emergency closure record physical restriction immediately, stop admission operationally and coordinate recovery.

## Alternatives and failures

Do not erase occupancy or displace occupants; unavailable communication cannot turn emergency reality into a rolled-back physical event.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-LOT-01, FR-LOT-02, FR-LOT-03, FR-LOT-04  
**US:** US-O05, US-OW01  
**Rules:** BR-CAP-01, BR-EMERG-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
