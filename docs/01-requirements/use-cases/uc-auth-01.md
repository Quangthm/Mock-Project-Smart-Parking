# UC-AUTH-01 — Register, authenticate and manage own profile

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner, Admin

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Register, authenticate and manage own profile.

## Preconditions

Registration/sign-in requested; profile access requires current authorization.

## Main flow

1. Register and verify a purpose-bound contact; apply 5-minute OTP expiry, three failures and 15-minute authentication lock.
2. Authenticate with password or either independently verified bound email/phone OTP; use declared SMS-fixture/email-capture mode for acceptance.
3. If TOTP is enabled, return only a restricted challenge and verify TOTP before issuing any normal session; if replacement is required, complete recovery first. Then issue 15-minute access and refresh session with 7-day absolute/24-hour inactivity limits; do not create roles or bypass pending Owner approval.
4. Edit own permitted profile fields; reverify changed contacts, use dedicated password change, and terminate renewal on logout.

## Alternatives and failures

Missing/unverified contact, expired challenge, locked account or revoked/cross-account grant rejects the protected action. TOTP is optional current MVP and cannot be bypassed once enabled; authentication lock is distinct from booking restrictions.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-AUTH-01, FR-AUTH-02, FR-AUTH-03, FR-BAS-08  
**US:** US-A01, US-D08  
**Rules:** BR-AUTH-01, BR-AUTH-02, BR-AUTH-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.


## SEC-01 trace additions

FR-AUTH-08; BR-AUTH-04/07; US-SEC01. Lost Authenticator routes to UC-AUTH-05 or UC-AUTH-06; primary success alone never grants normal access for an enabled account.
