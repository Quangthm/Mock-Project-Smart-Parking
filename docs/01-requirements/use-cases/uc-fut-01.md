# UC-FUT-01 — Detect per-slot occupancy and violation candidates

**Status:** FUTURE — outside MVP  
**Actors:** Operator, recognition adapter

[Use-case index](README.md) · [SRS module](../srs/modules/10-ai-assistance.md)

## Goal

Detect per-slot occupancy and violation candidates.

## Preconditions

FUTURE only.

## Main flow

1. Evaluate representative multi-frame per-slot observations.
2. Represent uncertainty and retain permitted evidence.
3. Request human review of any alleged violation.

## Alternatives and failures

No current camera AI/accuracy or punishment authority.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-13, FR-INC-02  
**US:** US-O08  
**Rules:** BR-VIOL-01

## Remaining work

Deferred design/integration/evaluation as labelled; not an active acceptance gate.
