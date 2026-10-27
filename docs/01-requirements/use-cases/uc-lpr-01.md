# UC-LPR-01 — Recognize an uploaded plate image and review uncertainty

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Assigned Operator, IoT & Recognition

[Use-case index](README.md) · [SRS module](../srs/modules/10-ai-assistance.md)

## Goal

Recognize an uploaded plate image and review uncertainty.

## Preconditions

Confirmed general direction; detailed integration POST-v1.0.

## Main flow

1. Validate permitted image input and protected access.
2. Retain original recognition/evidence and explicit success/uncertain/invalid failure outcomes.
3. Require manual review for uncertainty and stop automatic mismatch passage.
4. Audit accepted session correction without altering global vehicle binding.

## Alternatives and failures

Threshold/equality/model/dataset/evaluation are deferred; recognition does not free capacity or prove passage.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-09  
**US:** US-O08  
**Rules:** BR-GATE-01, BR-PRIV-02

## Remaining work

Deferred design/integration/evaluation as labelled; not an active acceptance gate.
