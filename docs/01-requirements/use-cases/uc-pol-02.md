# UC-POL-02 — Manage platform integrations and recognition settings

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin

[Use-case index](README.md) · [SRS module](../srs/modules/04-reservations-allocation.md)

## Goal

Manage platform integrations and recognition settings.

## Preconditions

Explicit platform integration permission, not inherited lot-operation authority.

## Main flow

1. Configure domain-owned integration adapter settings through authorized interfaces.
2. Record scope, version and redacted audit; never expose secrets in ordinary diagnostics.
3. Declare sandbox/fixture/live modes and preserve ordinary UI/manual fallback.

## Alternatives and failures

Recognition threshold/model/equality configuration detail is POST-v1.0; no current live-hardware claim.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-LOT-06, FR-POL-09  
**US:** US-A02, US-A07  
**Rules:** BR-AUTH-01, BR-GATE-01

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
