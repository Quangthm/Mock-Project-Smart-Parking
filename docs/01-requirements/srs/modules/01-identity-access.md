# Identity Access — SRS v1.0 RC1

[Requirements index](../README.md) · [Traceability](../../requirements-traceability/README.md)

**Status:** review candidate, 13 October 2026; implementation/evidence not assessed. Source review section numbers below refer to the decision review, not this module numbering.

- Access token: **15 minutes**. Refresh session: **7-day absolute lifetime**, with **24-hour refresh inactivity limit**. Rotation must not silently extend the absolute session maximum. Enforcement contracts must define current authorization and expiry boundaries.
- **Driver, Operator, Owner and Admin may use passwordless OTP through email and phone.** The staff-password-only recommendation was not selected. Existing password login and registration/contact/reset verification remain.
- The login feature does not create an account, approve an Owner, grant roles, bypass account/permission state, or merge two accounts because contact input matches loosely. Verified contact binding and challenge purposes must remain explicit.
- **Either verified bound channel independently is approved for every role.** Do not require both OTPs for one login. Missing/unverified channels cannot be used; neither channel implicitly creates or merges an account.
- SEC-01 supersedes the TOTP deferral: optional TOTP enrollment, login challenge, backup-code recovery and Admin-assisted recovery are current MVP for every role. Turnstile remains separate from authentication and authorization.
- Local acceptance uses email capture and phone/SMS fixtures under §16.6. Both login paths must be tested end-to-end in that declared mode for every role. This proves application flow, not real email/SMS delivery; a real SMS provider is a separate integration decision/test.

Owner creates Operator accounts within their organization, activates through verified contact, and assigns/revokes eligible owned-lot access. No Admin approval per hire, self-promotion or cross-owner grant. Revocation blocks future protected actions, including replayed requests and subscriptions, while preserving history.

Reject conflicting active normalized-plate binding without disclosing the other account. Admin reviews verified contact, relevant registration/transfer evidence and scoped Operator findings; notify existing holder. Never transfer for nonresponse alone; unresolved cases stay pending/escalated. User & Access performs the guarded change and preserves historical associations.

Profile fields: own name/display name/avatar; email/phone use re-verification, password uses dedicated change flow; role/status/permissions never customer-editable. Multiple plated vehicles require raw+normalized plate, category and vehicle image. Contact/security lock, account access state and booking-only restriction are distinct facts; OTP success does not approve Owner status or create grants.

## Functional requirements

### FR-AUTH-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Notification Service  
**Stories:** US-D08

Register using email or phone and verify the contact with purpose-bound OTP: expiry 5 minutes, 3 failed attempts trigger a 15-minute authentication lock. Owner registration also requires company name, email and phone and Admin approval. No corporate email-domain restriction; no CCCD/GPLX collection.

### FR-AUTH-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner, Admin  
**Stories:** US-A01, US-D08

Support password login and passwordless OTP for Driver, Operator, Owner and Admin. Either verified bound email or verified bound phone independently suffices; missing/unverified channels cannot log in. Access token 15 minutes; refresh session absolute 7 days and inactivity 24 hours, with no extension of the absolute lifetime by rotation. Logout terminates renewal/session capability. Enforce current account and scoped grants; login grants no new role. Turnstile is selected. TOTP is optional current MVP for all four roles; once enabled, it must succeed after primary verification before normal session issuance. See FR-AUTH-07–11. Phone acceptance uses SMS fixtures until live delivery is separately integrated.

### FR-AUTH-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D08

Allow Driver to view/update permitted own-profile fields: name/display name and avatar through normal profile editing; email and phone only through dedicated re-verification flows; password through the separate change-password flow. Deny cross-account access, and never allow customer editing of role, account status or permissions.

### FR-AUTH-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Owner, Operator (account activation)  
**Stories:** US-OW03

Owner creates Operator accounts within their organization, activates through verified contact, and assigns/revokes eligible owned-lot access. No Admin approval per hire, self-promotion or cross-owner grant. Revocation blocks future protected actions, including replayed requests and subscriptions, while preserving history.

### FR-AUTH-05

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Admin  
**Stories:** US-A01

Admin account management: the domain contract. Admin approves Owner registration after required company name, email and phone information is provided; role changes remain audited.

### FR-AUTH-06

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner, Admin  
**Stories:** US-A01

For every protected operation, enforce current role/permission, applicable resource scope and current account access state. Revoked permissions and newly locked accounts do not authorize subsequent protected requests even when an existing authentication token remains valid.

### FR-VEH-01

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver  
**Stories:** US-D09

Driver may list/edit multiple own plated vehicles, recording raw input and canonical normalized plate, vehicle category and vehicle image. Retain Vietnamese plate-format validation direction from source; exact supported-format contract must follow the retained legal reference review. No fabricated permanent plate for non-plated visits; active normalized binding uniqueness and dispute workflow remain guarded.

### FR-VEH-02

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Admin  
**Stories:** US-A04, US-D09

Reject conflicting active normalized-plate binding without disclosing the other account. Admin reviews verified contact, relevant registration/transfer evidence and scoped Operator findings; notify existing holder. Never transfer for nonresponse alone; unresolved cases stay pending/escalated. User & Access performs the guarded change and preserves historical associations.

### FR-VEH-03

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Operator  
**Stories:** US-D09, US-O01

Supported non-plated vehicle categories may be checked in through an Operator-managed parking session without a license plate. The system records the vehicle category, parking-session/ticket reference, actual entry/exit times, applicable capacity consumption and payment obligation; the generated session/ticket reference identifies the parking visit and is not a permanent vehicle identity. Motorcycle remains supported and EV charging is not a system function.

### FR-VEH-04

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator  
**Stories:** US-D01, US-O01

Validate vehicle/slot compatibility under the domain contract using approved vehicle and slot compatibility categories for MVP. Exact physical-dimension validation is not assumed unless separately approved for a parking configuration.

### FR-BAS-08

**Status:** CONFIRMED — implementation pending / not assessed  
**Actors:** Driver, Operator, Owner, Admin  
**Stories:** US-A01, US-D08

Support password login and passwordless OTP for Driver, Operator, Owner and Admin. Either verified bound email or verified bound phone independently suffices; missing/unverified channels cannot log in. Access token 15 minutes; refresh session absolute 7 days and inactivity 24 hours, with no extension of the absolute lifetime by rotation. Logout terminates renewal/session capability. Enforce current account and scoped grants; login grants no new role. Turnstile is selected. TOTP is optional current MVP for all four roles; once enabled, it must succeed after primary verification before normal session issuance. See FR-AUTH-07–11. Phone acceptance uses SMS fixtures until live delivery is separately integrated.


### FR-AUTH-07 — Optional TOTP enrollment and management

**Status:** CONFIRMED MVP — implementation and acceptance pending  
**Actors:** Driver, Operator, Owner, Admin  
**Stories:** US-SEC01

Every role may optionally enroll TOTP. Require recent primary authentication; pending enrollment does not enable TOTP until a correct six-digit code proves the new secret. Disabling requires recent primary authentication and the current TOTP code. Audit and notify enrollment/disable; invalidate factor-related challenges and revoke pre-change sessions on completion. Owner provisioning cannot enroll, read or disable an Operator’s factor on their behalf.


### FR-AUTH-08 — Staged login and challenge enforcement

**Status:** CONFIRMED MVP — implementation and acceptance pending  
**Actors:** Driver, Operator, Owner, Admin  
**Stories:** US-SEC01

Retain password or either verified email/phone OTP as the primary login route. If TOTP is enabled, primary success grants only a purpose-bound, short-lived challenge context; no normal access/refresh token, protected API access or SignalR subscription until TOTP succeeds. If replacement is required, no ordinary login is permitted until replacement completes. Current account/grant checks still apply. No remember-device bypass is introduced.


### FR-AUTH-09 — Backup-code recovery and replacement

**Status:** CONFIRMED MVP — implementation and acceptance pending  
**Actors:** Driver, Operator, Owner, Admin  
**Stories:** US-SEC02

Issue random single-use backup recovery codes after verified enrollment; display/download securely once and store only protected verification hashes. Primary verification plus one valid unused backup code starts a restricted Authenticator-replacement flow, not ordinary login. Consume the code atomically. Keep the old factor active until a new secret is proved; then atomically replace it, invalidate old backup codes/challenges, issue a fresh backup set and revoke old sessions. Failed/abandoned replacement must not silently disable the old factor. Regeneration requires recent full authentication and invalidates the previous set.


### FR-AUTH-10 — Account recovery tickets and Admin-assisted resets

**Status:** CONFIRMED MVP — implementation and acceptance pending  
**Actors:** Requester of any role, Admin  
**Stories:** US-SEC02, US-SEC03

Add ACCOUNT_RECOVERY tickets, submitted through a limited pre-login form or authenticated support. Notification & Audit owns ticket coordination; User & Access owns verification and credential mutations. Submission uses generic acknowledgement, rate/abuse controls and restricted case access; it neither discloses account existence nor grants login. Admin must verify at least one previously verified email/phone plus independently review supporting account evidence against existing records. OTP alone is insufficient. With neither channel accessible or insufficient evidence, keep pending/escalated; no automatic reset or new routine national-ID collection.


### FR-AUTH-11 — Scoped Admin recovery execution

**Status:** CONFIRMED MVP — implementation and acceptance pending  
**Actors:** Admin, affected account holder  
**Stories:** US-SEC03

Admin authorizes a single-use password and/or TOTP recovery flow linked to a reviewed ticket, with current permission, recent full authentication, target, reason and evidence references. User chooses the new password; Admin never sees or sets a known password. Password reset alone leaves TOTP enabled. TOTP reset invalidates old secret/backup codes, revokes sessions and enters REPLACEMENT_REQUIRED; only verified replacement enrollment restores ordinary login. Password mutation also revokes sessions. Admin accounts require a different Admin; self-approval is prohibited. When no other Admin is available, use a separately controlled deployment-maintainer runbook, never a public bypass. Recovery does not clear suspension, Owner-approval status or grant roles.


## Recovery authority

SEC-01/SEC-02 are approved by the PM on 13 October 2026. [Authentication recovery contract](../authentication-recovery.md) defines challenge states, evidence handling and recovery boundaries. These additions change no parking, money, monthly-pass or lot-operation permissions.
