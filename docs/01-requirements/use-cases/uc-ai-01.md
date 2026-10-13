# UC-AI-01 — Ask general help, search and explain prices/ranking

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver, Assistant

[Use-case index](README.md) · [SRS module](../srs/modules/10-ai-assistance.md)

## Goal

Ask general help, search and explain prices/ranking.

## Preconditions

Confirmed assistive scope; integration detail POST-v1.0.

## Main flow

1. Clarify missing location/vehicle/time parameters.
2. Answer FAQ from Admin-published versioned content.
3. Search/read current authorized APIs; explain deterministic results and backend prices.
4. Return explicit empty/error/stale outcomes and ordinary UI fallback.

## Alternatives and failures

No model-generated fees/ranking/permissions; no draft policy presented as active.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-AI-01, FR-AI-02, FR-AI-03, FR-AI-04, FR-AI-09, FR-AI-10, FR-AI-11  
**US:** US-D12  
**Rules:** BR-AI-01, BR-AI-02, BR-AI-04, BR-AI-05

## Remaining work

Deferred design/integration/evaluation as labelled; not an active acceptance gate.
