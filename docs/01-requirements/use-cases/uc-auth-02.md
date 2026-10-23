# UC-AUTH-02 — Review Partner Registration and administer permitted account access

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin, prospective Owner

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Review Partner Registration and administer permitted account access.

## Preconditions

Admin has current platform account-review permission.

## Main flow

1. Admin reviews saved Partner/first-lot setup, mandatory simulation and verified email. Phone is supplied but verification is not an approval prerequisite.
2. If externally agreed, replace only uploaded agreement/policy documents on the initial pending application; preserve original/replacements, actor/time/reason. Other fields and pending additional-lot documents cannot be edited.
3. Approve or reject against the reviewed application/document version. Final record is immutable; record rejection reason. No corrections/re-acceptance workflow or third outcome.
4. Persist approval independently from first-lot provisioning. Track pending/failed/unknown provisioning and safely retry with stable identities; successful initial provisioning is required before normal Owner login. Operational opening still requires readiness.
5. Email the decision; after replacement approval send “Your application was approved with some modifications”. Delivery retry preserves committed results and never repeats provisioning. Rejection is safely explained; the same initial email cannot register again.
6. For other administratively disabled accounts, explicitly re-enable with reason, removing only that restriction. Never approve/reverify/regrant/revive sessions or clear independent restrictions through re-enable.
7. Protect the sole seeded Admin from administrative disable/lock/delete/role/authority change, including self and last-Admin loss. Own ordinary profile/security is allowed, authentication lockouts apply, and recovery uses the maintainer procedure. No role management or Admin registration in MVP.

## Alternatives and failures

Reject stale review/document versions and unauthorized changes; current services enforce revocation despite old tokens. Re-enable cannot reverse rejection. Failed provisioning is not rejection; failed notification does not undo approval.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-AUTH-05, FR-AUTH-06  
**US:** US-A01, US-OW01
**Rules:** BR-AUTH-01, BR-AUTH-02, BR-AUTH-03, BR-PRIV-01, BR-PRIV-02, BR-PRIV-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.


## SEC-02 account recovery actions

Admin may review ACCOUNT_RECOVERY and authorize password/TOTP reset under UC-AUTH-06 and FR-AUTH-10/11. The sole seeded Admin uses controlled maintainer recovery; no self-approval or Owner/Operator inheritance. Password reset is a single-use user-selected password flow; TOTP reset mandates replacement enrollment. No secret disclosure or account/grant unblock is implied.
