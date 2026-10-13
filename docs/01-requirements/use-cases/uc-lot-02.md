# UC-LOT-02 — Manage assigned-lot devices and view status

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Assigned Operator, Owner

[Use-case index](README.md) · [SRS module](../srs/modules/02-facility-capacity.md)

## Goal

Manage assigned-lot devices and view status.

## Preconditions

Current lot scope; fixture-mode devices in local acceptance.

## Main flow

1. Operator manages linked devices and enabled state.
2. Display connectivity, last observation and errors with simulated-source labels.
3. Owner inspects owned-lot diagnostics read-only.

## Alternatives and failures

Read permission never mutates devices; real hardware adapters remain FUTURE.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-LOT-05, FR-LOT-07  
**US:** US-O03, US-O05, US-OW05  
**Rules:** BR-AUTH-01, BR-FAIL-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
