# UC-AUTH-03 — Provision Operators and lot-scoped access

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Provision Operators and lot-scoped access.

## Preconditions

Owner is approved and controls the target organization/lots.

## Main flow

1. Owner creates Operator account in their organization.
2. Verify contact and activate through the supported identity flow; no Admin approval per hire.
3. Owner assigns/revokes owned-lot permissions; record effective grants and audit.
4. Subsequent commands, cached private-result access and subscriptions recheck current grants.

## Alternatives and failures

Cross-owner assignment, self-promotion and broader-than-Owner grants are rejected; revocation preserves historical attribution and already-authorized durable financial work.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-AUTH-04  
**US:** US-OW03  
**Rules:** BR-AUTH-01, BR-AUTH-02

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.
