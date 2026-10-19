# Identity Access — business rules RC1

[Rule index](README.md) · [SRS module](../srs/modules/01-identity-access.md)

## BR-AUTH-01

**Status:** CONFIRMED — implementation pending / not assessed

Owner creates Operator accounts within their organization, activates through verified contact, and assigns/revokes eligible owned-lot access. No Admin approval per hire, self-promotion or cross-owner grant. Revocation blocks future protected actions, including replayed requests and subscriptions, while preserving history. For every protected operation, enforce current role/permission, applicable resource scope and current account access state. Revoked permissions and newly locked accounts do not authorize subsequent protected requests even when an existing authentication token remains valid. No blanket Admin operational authority; see permission matrix.

**Trace:** FR-AUTH-02/04/05/06; UC-AUTH-01, UC-AUTH-02, UC-AUTH-03, UC-LOT-02, UC-POL-02, UC-RPT-01

## BR-AUTH-02

**Status:** CONFIRMED — implementation pending / not assessed

Register using email or phone and verify the contact with purpose-bound OTP: expiry 5 minutes, 3 failed attempts trigger a 15-minute authentication lock. Owner registration also requires company name, email and phone and Admin approval. No corporate email-domain restriction; no CCCD/GPLX collection. Support password login and passwordless OTP for Driver, Operator, Owner and Admin. Either verified bound email or verified bound phone independently suffices; missing/unverified channels cannot log in. Access token 15 minutes; refresh session absolute 7 days and inactivity 24 hours, with no extension of the absolute lifetime by rotation. Logout terminates renewal/session capability. Enforce current account and scoped grants; login grants no new role. Turnstile is selected. TOTP is optional current MVP for all four roles; enabled accounts require the additional factor before normal login. FR-AUTH-07–11 and BR-AUTH-04–07 apply. Phone acceptance uses SMS fixtures until live delivery is separately integrated.

**Trace:** FR-AUTH-01/04, FR-BAS-08; UC-AUTH-01, UC-AUTH-02, UC-AUTH-03

## BR-AUTH-03

**Status:** CONFIRMED — implementation pending / not assessed

Read/write access is checked against authenticated identity and the particular resource; Operator may access only assigned lot/lane/case data, not the global customer database.

**Trace:** FR-AUTH-03/06, FR-AI-05; UC-AUTH-01, UC-AUTH-02, UC-PAY-02

## BR-VEH-01

**Status:** CONFIRMED — implementation pending / not assessed

Driver may list/edit multiple own plated vehicles, recording raw input and canonical normalized plate, vehicle category and vehicle image. Retain Vietnamese plate-format validation direction from source; exact supported-format contract must follow the retained legal reference review. No fabricated permanent plate for non-plated visits; active normalized binding uniqueness and dispute workflow remain guarded. Supported non-plated vehicle categories may be checked in through an Operator-managed parking session without a license plate. The system records the vehicle category, parking-session/ticket reference, actual entry/exit times, applicable capacity consumption and payment obligation; the generated session/ticket reference identifies the parking visit and is not a permanent vehicle identity. Motorcycle remains supported and EV charging is not a system function.

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

ACCOUNT_RECOVERY is an Admin-handled support type. Verify a previously bound verified channel and independently review evidence against stored account records; a matching email, OTP or screenshot alone is not approval. Missing channel access/evidence keeps the case pending/escalated. Admin authorizes scoped single-use reset; user selects password, password-only reset preserves TOTP, and TOTP reset requires replacement enrollment. Different Admin handles an Admin target; no self-approval. Record decision separately from successful credential mutation.

**Trace:** FR-AUTH-07–11; UC-AUTH-01, UC-AUTH-04, UC-AUTH-05, UC-AUTH-06; US-SEC01–03.


## BR-AUTH-07

**Status:** CONFIRMED MVP — implementation and acceptance pending

Credential changes and session issuance serialize on User & Access security/factor versions. Reject used TOTP time steps, consumed backup codes, expired/stale challenges and reused grants. Revocation must prevent subsequent protected actions, not merely refresh: enforce current security/session version at resource services. Pending grants grant no role or normal session. Audit only identifiers, actor/scope/reason/outcome and evidence references; never log passwords, TOTP secrets, QR payloads, codes or reset tokens.

**Trace:** FR-AUTH-07–11; UC-AUTH-01, UC-AUTH-04, UC-AUTH-05, UC-AUTH-06; US-SEC01–03.
