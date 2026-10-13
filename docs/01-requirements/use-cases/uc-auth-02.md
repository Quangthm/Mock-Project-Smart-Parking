# UC-AUTH-02 — Approve Owner registration and administer accounts

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin, prospective Owner

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Approve Owner registration and administer accounts.

## Preconditions

Admin has current platform account-review permission.

## Main flow

1. Review required company name, email and phone and verification/access state.
2. Approve Owner registration and record audited actor, decision and time; no corporate-domain condition.
3. Manage explicitly authorized account/role security actions without inheriting lot operational powers.

## Alternatives and failures

Reject unsupported role changes; current resource services enforce revocations even for valid existing tokens.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-AUTH-05, FR-AUTH-06  
**US:** US-A01  
**Rules:** BR-AUTH-01, BR-AUTH-02, BR-AUTH-03, BR-PRIV-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.


## SEC-02 account recovery actions

Admin may review ACCOUNT_RECOVERY and authorize password/TOTP reset under UC-AUTH-06 and FR-AUTH-10/11. Require independent Admin for Admin target; no self-approval or Owner/Operator inheritance. Password reset is a single-use user-selected password flow; TOTP reset mandates replacement enrollment. No secret disclosure or account/grant unblock is implied.
