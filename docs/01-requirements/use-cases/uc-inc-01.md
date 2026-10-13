# UC-INC-01 — Record an incident or suspected violation and override safely

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Assigned Operator

[Use-case index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md)

## Goal

Record an incident or suspected violation and override safely.

## Preconditions

Relevant lot incident or suspected wrong-slot observation.

## Main flow

1. Record incident context/time/evidence/handler and REPORTED state.
2. Review evidence and allocation history; violation finding goes SUSPECTED → IN_REVIEW → CONFIRMED or DISMISSED.
3. Record authorized assistance/correction/override with reason and permitted consequence.
4. Resolve and close incident once outcome/recovery is recorded.

## Alternatives and failures

Missing evidence remains pending unless approved emergency with missing-evidence incident. No automatic fine, ban, towing or AI punitive authority; alternative allocation is not wrong-slot use.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-INC-01, FR-INC-03, FR-INC-04, FR-INC-06, FR-OPS-03  
**US:** US-O05  
**Rules:** BR-VIOL-02, BR-VIOL-03, BR-EMERG-01, BR-EMERG-04

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
