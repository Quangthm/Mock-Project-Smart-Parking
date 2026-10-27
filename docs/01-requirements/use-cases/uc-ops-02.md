# UC-OPS-02 — Authorize offline and replay queued events

**Status:** FUTURE — outside MVP  
**Actors:** Assigned Operator, device adapter

[Use-case index](README.md) · [SRS module](../srs/modules/05-sessions-operations.md)

## Goal

Authorize offline and replay queued events.

## Preconditions

FUTURE only; not enabled in MVP.

## Main flow

1. Future design must authorize cached scope securely.
2. Durably capture offline operations and replay with idempotency.
3. Reconcile competing physical/financial facts before claiming recovery.

## Alternatives and failures

Not current offline authority. Current outage flow records incident, avoids false success and reconciles actual events after connectivity returns.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-OPS-04, FR-OPS-05  
**US:** US-O06  
**Rules:** BR-FAIL-01, BR-FAIL-02

## Remaining work

Deferred design/integration/evaluation as labelled; not an active acceptance gate.
