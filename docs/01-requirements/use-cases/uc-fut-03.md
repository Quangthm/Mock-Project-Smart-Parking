# UC-FUT-03 — Forecast capacity and investigate anomalies

**Status:** FUTURE — outside MVP  
**Actors:** Owner, Operator

[Use-case index](README.md) · [SRS module](../srs/modules/10-ai-assistance.md)

## Goal

Forecast capacity and investigate anomalies.

## Preconditions

FUTURE forecasting/anomaly models only.

## Main flow

1. Define permitted dataset/model/version and evaluation.
2. Produce forecast or anomaly candidate with uncertainty.
3. Authorized human investigates; domain commands retain guards.

## Alternatives and failures

No automatic pricing change, punitive action or claim of achieved accuracy.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-14, FR-BAS-15  
**US:** US-O10, US-OW08  
**Rules:** BR-AI-04

## Remaining work

Deferred design/integration/evaluation as labelled; not an active acceptance gate.
