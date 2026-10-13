# UC-OPS-01 — Handle emergency release and service interruption

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Assigned Operator, Owner escalation

[Use-case index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md)

## Goal

Handle emergency release and service interruption.

## Preconditions

Approved emergency/recovery condition with explicit current permission.

## Main flow

1. Record reason, incident, resource/version, grant and money state.
2. Execute safe scoped emergency action; record actual movement independently from gate authorization.
3. Retain debt/attempts and any missing-evidence incident.
4. Reconcile physical and financial facts; Owner handles escalation.

## Alternatives and failures

UNKNOWN possible payment success prevents blind unpaid checkout invalidation; paid visit cannot reopen. No automatic forced displacement or blanket Admin override.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-OPS-01, FR-OPS-02, FR-OPS-06  
**US:** US-O04, US-O05  
**Rules:** BR-EMERG-02, BR-EMERG-03, BR-FAIL-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
