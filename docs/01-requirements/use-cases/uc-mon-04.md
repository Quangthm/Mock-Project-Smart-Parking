# UC-MON-04 — Validate monthly/yearly visits and bill uncovered use

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Assigned Operator, Driver

[Use-case index](README.md) · [SRS module](../srs/modules/07-monthly-passes.md)

## Goal

Validate monthly/yearly visits and bill uncovered use.

## Preconditions

Valid bound pass or ordinary reservation/visit.

## Main flow

1. Validate account/vehicle/location/period/current rights and actual compatible admission capacity.
2. Record one session/occupancy; available-only pass never forces full-lot entry.
3. At checkout exclude valid day/night covered intervals; use whole-session 15-minute test and accepted cutoff.
4. Apply eligible monthly/yearly coverage before any separately booked minimum; retain independent extras and credited money.

## Alternatives and failures

60 covered +10 uncovered charges uncovered10; no grace restart. Expiry does not free space; zero-price reservation needs ordinary capacity claim and explicit no-payment basis.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-MON-07, FR-MON-08, FR-RES-04  
**US:** US-D14, US-O07  
**Rules:** M-04, M-07, M-08, BR-GATE-02

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
