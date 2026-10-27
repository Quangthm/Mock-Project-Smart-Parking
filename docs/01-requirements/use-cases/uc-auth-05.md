# UC-AUTH-05 — Recover Authenticator using backup code

**Status:** CONFIRMED MVP — implementation and acceptance pending

[Use-case index](README.md) · [Canonical security contract](../srs/authentication-recovery.md)

## Actors and preconditions

Any of the four account roles acting on their own factor; purpose-bound verified context and current account-state guards.

## Main flow

1. User completes primary verification but cannot supply current TOTP.
2. Validate and atomically consume an unused backup code; create restricted replacement context, no ordinary login.
3. Keep old factor until new Authenticator code proves pending replacement.
4. Atomically replace factor, invalidate old codes/challenges, issue fresh backup set and revoke old sessions.
5. Require normal completed login after recovery; display verified outcome and notify.

## Alternatives and failures

Two concurrent redemptions have one winner. Timeout looks up same operation. Failed replacement leaves old factor active; expired context does not restore consumed code. With no usable backup, submit UC-AUTH-06.

## Postconditions and traceability

Persist guarded outcome once; pending/failed recovery never shown as successful login. Implementation/acceptance pending.

**FR:** FR-AUTH-09  
**Rules:** BR-AUTH-05, BR-AUTH-07  
**US:** US-SEC02
