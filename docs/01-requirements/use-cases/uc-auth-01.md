# UC-AUTH-01 — Register, authenticate and manage own profile

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner, Admin

[Use-case index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## Goal

Register, authenticate and manage own profile.

## Preconditions

Registration/sign-in requested; profile access requires current authorization.

## Main flow

1. Driver registers a pending identity, consents to mandatory clearly labelled VNeID simulation, receives the fake API GUID substitute associated with that user and persisted by User & Access, and verifies a purpose-bound contact. The GUID is neither a government identity number nor an authentication token. Apply 5-minute OTP expiry, three failures and 15-minute authentication lock; verification does not automatically log in.
2. For Partner Registration keep Owner role: immediately persist pending identity/application, complete required simulation with its fake-API GUID substitute associated with the Owner user and persisted by User & Access, and save first-lot setup with uploaded agreement documents before email OTP issuance. Verify email before combined review under UC-AUTH-02; phone verification is separate before phone OTP login.
3. Pending applicant uses purpose-scoped setup/verification/resend/recovery credentials; application ID alone grants nothing. Require combined approval and successful initial provisioning before normal Owner sign-in.
4. Authenticate with password or either independently verified bound email/phone OTP; use declared SMS-fixture/email-capture mode. Complete enabled TOTP or replacement before normal session issuance.
5. Normal success returns necessary tokens only: access 15 minutes, refresh absolute 7 days/inactivity 24 hours. Retrieve authorized profile/onboarding separately, such as /me. Safe actionable failures and required MFA challenge data remain.
6. Edit own permitted fields; reverify contacts, use dedicated password change and terminate renewal on logout. Operator activation follows UC-AUTH-03; sole seeded Admin has no registration route and is the only simulation exemption.

## Alternatives and failures

Failed/declined simulation never completes registration/activation. Pending/rejected review and pending/failed provisioning block normal Owner login with safe reasons after sufficient credential validation. Initial rejected Partner email cannot register again. Existing account, grant and MFA gates remain; no raw sensitive error details.

## Postconditions

Persist the accepted outcome and history once; failed/unknown work is shown honestly and retains recoverable rights, money and physical facts. No implementation/test completion is implied.

## Traceability

**FR:** FR-AUTH-01, FR-AUTH-02, FR-AUTH-03, FR-BAS-08  
**US:** US-A01, US-D08, US-OW01
**Rules:** BR-AUTH-01, BR-AUTH-02, BR-AUTH-03

## Remaining work

Implementation and acceptance evidence pending; use the policy, permission and transition contracts without inventing business defaults.


## SEC-01 trace additions

FR-AUTH-08; BR-AUTH-04/07; US-SEC01. Lost Authenticator routes to UC-AUTH-05 or UC-AUTH-06; primary success alone never grants normal access for an enabled account.
