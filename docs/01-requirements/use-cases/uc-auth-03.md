# UC-AUTH-03 — Provision Operators and lot-scoped access

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Provision Operators and lot-scoped access.

## Preconditions

Owner is approved and controls the target organization/lots.

## Main flow

1. Approved, successfully provisioned Owner creates a pending Operator in the owned organization; persist the invitation even if email delivery fails.
2. Operator follows email activation, verifies contact and completes mandatory consented VNeID simulation. The fake API returns a GUID substitute bound to that user and persisted in the User & Access database; the GUID is not a government identity number or an authentication token. Password setup is optional; an eligible verified channel supports OTP login.
3. Owner assigns/revokes owned-lot permissions; no Admin approval per hire, immediate active account or cross-owner grant.
4. Subsequent normal login completes current account/MFA checks; commands, private replays and subscriptions recheck current grants.

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
