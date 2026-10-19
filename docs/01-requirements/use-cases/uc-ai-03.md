# UC-AI-03 — Escalate conversation to human support

**Status:** CONFIRMED DIRECTION — detailed integration/evaluation POST-v1.0  
**Actors:** Driver, assigned Operator or Admin

[Use-case index](README.md) · [SRS module](../srs/modules/10-ai-assistance.md)

## Goal

Escalate conversation to human support.

## Preconditions

User requests asynchronous help; contextual scope authorized.

## Main flow

1. Create General Support ticket in Notification & Audit with minimal scoped references.
2. Route lot matter to Operator, account/platform/global binding matter to Admin.
3. Handler advances OPEN/IN_PROGRESS/WAITING_FOR_CUSTOMER/RESOLVED/CLOSED with history.
4. Show actual ticket state; keep appeal and financial workflows independent.

## Alternatives and failures

No whole-chat copy or staffed live-chat promise. Assistant integration detail is POST-v1.0; actual published hours/rotation require PM declaration.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-BAS-10  
**US:** US-D12, US-O09  
**Rules:** BR-AI-02, BR-PRIV-01

## Remaining work

Deferred design/integration/evaluation as labelled; not an active acceptance gate.
