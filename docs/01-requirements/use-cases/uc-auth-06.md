# UC-AUTH-06 — Request and execute Admin-assisted account recovery

**Status:** CONFIRMED MVP — implementation and acceptance pending

[Use-case index](README.md) · [Canonical security contract](../srs/authentication-recovery.md)

## Actors and preconditions

Any account holder; Admin with explicit recovery permission for assisted actions. Current account/grant guards apply; pre-login submission is only a restricted support exception.

## Main flow

1. Requester submits limited pre-login/authenticated ACCOUNT_RECOVERY form; generic acknowledgement and no account session.
2. Admin verifies access to a previously verified contact and independently reviews supporting evidence against existing records; record findings.
3. The sole seeded Admin uses the controlled maintainer procedure; reject self-approval and do not create a second Admin registration flow.
4. Admin with current permission/recent full authentication authorizes explicit PASSWORD/TOTP/BOTH single-use flow.
5. User & Access executes: user chooses password, preserving TOTP for password-only; TOTP reset invalidates old factor/codes, revokes sessions and requires verified new enrollment.
6. Record actual credential outcome; notify; resolve successful recovery only after required changes complete.

## Alternatives and failures

Insufficient evidence/no existing-channel access stays pending/escalated. Grant timeout/delivery failure does not mean success or disable replacement guard. Recovery never grants roles, removes account block or bypasses Owner approval.

## Postconditions and traceability

Persist guarded outcome once; pending/failed recovery never shown as successful login. Implementation/acceptance pending.

**FR:** FR-AUTH-10, FR-AUTH-11  
**Rules:** BR-AUTH-06, BR-AUTH-07  
**US:** US-SEC02, US-SEC03
