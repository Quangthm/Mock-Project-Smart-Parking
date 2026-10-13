# UC-AI-02 — Read own records and prepare a confirmed reservation action

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver, Assistant

[Use-case index](README.md) · [SRS module](../srs/modules/10-ai-assistance.md)

## Goal

Read own records and prepare a confirmed reservation action.

## Preconditions

Authenticated requesting-user scope; integration detail POST-v1.0.

## Main flow

1. Read own permitted vehicles/reservations/visit/money states via allowlisted APIs.
2. Prepare reservation form without submitting an autonomous mutation.
3. Driver reviews exact terms in normal UI; backend revalidates and executes normal command.
4. Show actual result and normal checkout.

## Alternatives and failures

Changed terms require reconfirmation; cancellation/refund/security actions route to ordinary screens. UNKNOWN never becomes fabricated success.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-AI-05, FR-AI-06, FR-AI-07, FR-AI-08, FR-BAS-11  
**US:** US-D12  
**Rules:** BR-AI-02, BR-AI-03, BR-AI-04, BR-POL-04

## Remaining work

Deferred design/integration/evaluation as labelled; not an active acceptance gate.
