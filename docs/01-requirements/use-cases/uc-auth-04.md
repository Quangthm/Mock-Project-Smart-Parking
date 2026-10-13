# UC-AUTH-04 — Enroll, disable and manage TOTP

**Status:** CONFIRMED MVP — implementation and acceptance pending

[Use-case index](README.md) · [Canonical security contract](../srs/authentication-recovery.md)

## Actors and preconditions

Any of the four account roles acting on their own factor; purpose-bound verified context and current account-state guards.

## Main flow

1. Account holder of any role reauthenticates; show optional security setting and pending Authenticator QR/secret.
2. User proves a six-digit code; activate only on valid pending-version proof and generate backup codes for secure one-time display.
3. For disable, verify recent primary proof and current TOTP; invalidate factor/codes/challenges and revoke pre-change sessions.
4. Audit and notify; permit subsequent ordinary login according to the new factor state.

## Alternatives and failures

Invalid/expired proof leaves prior state unchanged; abandoned enrollment never enables. Replayed proof or stale factor version rejects. Owner cannot manage another Operator’s factor.

## Postconditions and traceability

Persist guarded outcome once; pending/failed recovery never shown as successful login. Implementation/acceptance pending.

**FR:** FR-AUTH-07, FR-AUTH-08  
**Rules:** BR-AUTH-04, BR-AUTH-07  
**US:** US-SEC01
