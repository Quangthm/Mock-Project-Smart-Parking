# Identity Access — business rules v1.1

[Rule index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## BR-AUTH-01

**Status:** CONFIRMED — implementation pending / not assessed

Owner creates a pending Operator within the owned organization and sends an email activation link. The Operator verifies contact and completes mandatory, consented simulated VNeID during activation; the fake API returns a user-associated GUID substitute persisted by User & Access, not an authentication token or government identity number. Password setup is optional, preserving OTP login eligibility. No immediate operational activation, Admin approval per hire, self-promotion or cross-owner grant. Owner assigns/revokes eligible owned-lot access. Current account/grant checks block subsequent protected actions, replayed private results and subscriptions after revocation while preserving history.

Admin reviews one combined Partner/first-lot application only after saved setup, mandatory simulation and verified email. Approval and rejection are the only final review outcomes. Before final review, Admin may replace only uploaded agreement/policy documents for the initial pending application; preserve original and every replacement with actor, timestamp and reason. Bind approval to the reviewed application/document versions and reject concurrent stale changes. Other fields and pending additional-lot applications cannot be edited by Admin. Finalized approved/rejected applications are immutable. Discussions happen outside SmartPark; no in-app corrections or re-acceptance workflow is introduced.

Approval after document replacement sends “Your application was approved with some modifications”; this remains approval, not a third outcome. Approval commits separately from provisioning; visible pending/failed/unknown provisioning is retry-safe and cannot create duplicate organization/first-lot records. Initial provisioning must succeed before normal Owner login; notify approval by email without implying sign-in readiness or automatic operational opening. Do not ask the approved Owner to create the first lot again.

Rejection records a reason and sends a safe email explanation and appropriate protected login-failure reason. In MVP the same normalized rejected initial Partner email cannot register again. Re-enable cannot reverse rejection. Notification failure preserves the created identity/application and committed decision; track queued/pending/failed/delivered states and retry delivery without repeating business work.

MVP has exactly one protected seeded Admin, exempt from VNeID simulation, and no Admin registration route or role-management feature. Reject administrative disable, lock, delete, self-escalation and role/authority changes targeting it; this also protects the last eligible Admin atomically. Own ordinary profile/security changes remain allowed, authentication lockouts still apply, and recovery uses the controlled maintainer procedure. Fixed role assignment in approved registration/activation is not role management.

An authorized Admin may explicitly re-enable another administratively disabled account with a mandatory reason. Remove only that disable restriction: do not verify contacts, approve a pending/rejected Owner, restore removed roles/grants, clear independent authentication/security/booking restrictions or revive revoked sessions. For every protected operation, enforce current role/permission, applicable resource scope and current account access state. Revoked permissions and newly locked accounts do not authorize subsequent protected requests even when an existing authentication token remains valid. No blanket Admin operational authority; see permission matrix.

**Trace:** FR-AUTH-02/04/05/06; UC-AUTH-01, UC-AUTH-02, UC-AUTH-03, UC-LOT-02, UC-POL-02, UC-RPT-01

## BR-AUTH-02

**Status:** CONFIRMED — implementation pending / not assessed

Driver registration creates a pending identity and requires consented, clearly labelled simulated VNeID information sharing plus purpose-bound contact verification. The fake VNeID API returns a GUID substitute associated with the user; User & Access persists that association and GUID in its database. This is a simulated identifier, not a government identity number, authentication token or authorization credential. Additional mock fields, consent/completion evidence encoding, retention and exact API/schema details remain design-pending under V11-DES-02; no route or database field name is approved here. OTP expiry is 5 minutes; 3 failed attempts trigger a 15-minute authentication lock. Verification does not automatically sign in the Driver: use the shared login and MFA gates.

Partner Registration retains the existing Owner role; no Partner role is added. Persist pending User/Account and the initial application immediately. Collect company name, email and phone without a corporate-domain restriction. The initial application includes first-lot setup and uploaded agreement/policy documents. Complete mandatory simulation during registration, save setup, then issue the email OTP, verify email, and submit the combined application for Admin review. Enforce setup, simulation and verified-email prerequisites server-side. Phone need not be verified for approval but must be verified separately before phone OTP login.

Pending identity is not an operational Owner session. Purpose-scoped setup/verification/resend credentials and secure recovery authorize only those operations; application ID alone is not authorization. Normal login requires approval and successful initial provisioning. Preserve distinct contact, account-access, application-review, provisioning and lot-open facts. No routine real CCCD/GPLX collection or government verification is introduced by the simulation. Support password login and passwordless OTP for Driver, Operator, Owner and Admin. Either verified bound email or verified bound phone independently suffices; missing/unverified channels cannot log in. Access token 15 minutes; refresh session absolute 7 days and inactivity 24 hours, with no extension of the absolute lifetime by rotation. Logout terminates renewal/session capability. Enforce current account and scoped grants; login grants no new role. Turnstile is selected. TOTP is optional current MVP for all four roles; enabled accounts require the additional factor before normal login. FR-AUTH-07–11 and BR-AUTH-04–07 apply. Phone acceptance uses SMS fixtures until live delivery is separately integrated.

Normal completed login returns necessary tokens; retrieve authorized account/profile/onboarding information separately (for example through /me), without unconditional profile/onboarding payloads. A required MFA challenge remains a restricted intermediate response, not normal login. Safe standardized actionable failures retain reasons for pending review, rejection and pending/failed initial provisioning after sufficient credential validation; do not reveal raw exceptions, secrets or an account-enumeration oracle. Response envelope, delivery and credential-validation threshold remain API/security design work.

**Trace:** FR-AUTH-01/04, FR-BAS-08; UC-AUTH-01, UC-AUTH-02, UC-AUTH-03

## BR-AUTH-03

**Status:** CONFIRMED — implementation pending / not assessed

Read/write access is checked against authenticated identity and the particular resource; Operator may access only assigned lot/lane/case data, not the global customer database.

**Trace:** FR-AUTH-03/06, FR-AI-05; UC-AUTH-01, UC-AUTH-02, UC-PAY-02

## BR-VEH-01

**Status:** CONFIRMED — implementation pending / not assessed

Driver may list/edit multiple own plated vehicles, recording raw input and canonical normalized plate, vehicle category and vehicle image. Retain Vietnamese plate-format validation direction from source; exact supported-format contract must follow the retained legal reference review. No fabricated permanent plate for non-plated visits; active normalized binding uniqueness and dispute workflow remain guarded. Supported non-plated vehicle categories may be checked in through an Operator-managed parking session without a license plate. The system records the vehicle category, parking-session/ticket reference, actual entry/exit times, applicable capacity consumption and payment obligation; the generated session/ticket reference identifies the parking visit and is not a permanent vehicle identity. Motorcycle remains supported and EV charging is not a system function.

Vehicle removal is soft removal only. Reject removal while active reservations, visits, pass coverage or other live dependencies would be invalidated; handle them through their approved workflows first and recheck at commit. Preserve historical account/vehicle/plate associations and attribution. Removal is not transfer approval; hard deletion/cleanup follows approved retention and holds.

**Trace:** FR-VEH-01/03; UC-VEH-01

## BR-VEH-02

**Status:** CONFIRMED — implementation pending / not assessed

Reject conflicting active normalized-plate binding without disclosing the other account. Admin reviews verified contact, relevant registration/transfer evidence and scoped Operator findings; notify existing holder. Never transfer for nonresponse alone; unresolved cases stay pending/escalated. User & Access performs the guarded change and preserves historical associations.

**Trace:** FR-VEH-02; UC-VEH-02

## BR-VEH-03

**Status:** CONFIRMED — implementation pending / not assessed

Compatibility is required by the domain contract. Supported motorcycle parking is part of the baseline. An EV-compatible slot is a slot type/attribute used for occupancy, allocation and payment only; charging functionality and charging-state validation are not implemented. Other size/type constraints remain refinement.

**Trace:** FR-VEH-04; UC-VEH-01


## BR-AUTH-04

**Status:** CONFIRMED MVP — implementation and acceptance pending

Optional TOTP applies equally to all four roles. Pending enrollment is not enabled. Primary authentication remains password or either verified bound OTP channel; enabled TOTP adds a mandatory second step before any normal session. A password/OTP login path cannot bypass an enabled or replacement-required factor. Disabling requires recent primary authentication plus current TOTP, with audit, notification and session revocation.

**Trace:** FR-AUTH-07–11; UC-AUTH-01, UC-AUTH-04, UC-AUTH-05, UC-AUTH-06; US-SEC01–03.


## BR-AUTH-05

**Status:** CONFIRMED MVP — implementation and acceptance pending

Backup codes are distinct from emailed/SMS OTPs and are single use. Primary verification plus an unused backup code authorizes only restricted replacement. Consumption and recovery intent are atomic; a timeout is resolved by operation lookup, not another code debit. Old factor remains until successful replacement, then old codes/challenges/sessions are invalidated. Codes cannot be retrieved in plaintext later; regeneration after recent full authentication replaces the entire set.

**Trace:** FR-AUTH-07–11; UC-AUTH-01, UC-AUTH-04, UC-AUTH-05, UC-AUTH-06; US-SEC01–03.


## BR-AUTH-06

**Status:** CONFIRMED MVP — implementation and acceptance pending

ACCOUNT_RECOVERY is an Admin-handled support type. Verify a previously bound verified channel and independently review evidence against stored account records; a matching email, OTP or screenshot alone is not approval. Missing channel access/evidence keeps the case pending/escalated. Admin authorizes scoped single-use reset; user selects password, password-only reset preserves TOTP, and TOTP reset requires replacement enrollment. The sole seeded Admin uses controlled maintainer recovery; no self-approval or additional Admin onboarding. Record decision separately from successful credential mutation.

**Trace:** FR-AUTH-07–11; UC-AUTH-01, UC-AUTH-04, UC-AUTH-05, UC-AUTH-06; US-SEC01–03.


## BR-AUTH-07

**Status:** CONFIRMED MVP — implementation and acceptance pending

Credential changes and session issuance serialize on User & Access security/factor versions. Reject used TOTP time steps, consumed backup codes, expired/stale challenges and reused grants. Revocation must prevent subsequent protected actions, not merely refresh: enforce current security/session version at resource services. Pending grants grant no role or normal session. Audit only identifiers, actor/scope/reason/outcome and evidence references; never log passwords, TOTP secrets, QR payloads, codes or reset tokens.

**Trace:** FR-AUTH-07–11; UC-AUTH-01, UC-AUTH-04, UC-AUTH-05, UC-AUTH-06; US-SEC01–03.
