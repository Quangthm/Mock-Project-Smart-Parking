# Driver registration and verification API design

SPARK-174 / SPARK-176 / SPARK-177 / SPARK-182 · SPARK-296 update, 21 October 2026. **v1.1 documentation review candidate based on approved decisions**; implementation and verification pending.

Canonical behavior: [FR-AUTH-01/02/06 and FR-BAS-08](../../01-requirements/srs/modules/01-identity-access.md), [UC-AUTH-01](../../01-requirements/use-cases/uc-auth-01.md), [shared login/security contract](../../02-architecture/api/v1.1-contracts.md). This contract replaces the competing `/driver-register` route proposals and auto-login examples.

## Endpoints and payloads

| Operation | Existing method/path | Request and result |
|---|---|---|
| Register | `POST /api/auth/register/driver` | `fullName`, at least one of `email`/`phone`, optional `password`; persist pending identity and Driver account, require consented mandatory VNeID simulation. Return scoped registration/challenge metadata; no session. |
| Verify | `POST /api/auth/register/driver/verify` | `registrationId`, six-digit `code`, authorized verification context; valid contact proof plus completed simulation permits activation. Return verification result only, **no automatic login** (D06). |
| Resend | `POST /api/auth/register/driver/resend` | Existing `registrationId`, authorized scoped context; replace OTP while preserving attempts/lock and cooldown. |
| Recover | `POST /api/auth/register/driver/recover` | Existing `contact`; safe recovery only, no secret/session issued solely from contact or ID. Final recovery authorization/envelope pending V11-DES-03. |

Existing registration fields/bounds retained: nonblank full name up to 255 characters, valid normalized email up to 255, valid phone with optional `+` and 9–15 digits, optional password following the existing 8–15-character upper/lower/digit/special policy. If both contacts are supplied, registration OTP uses email; only that channel becomes verified. Existing phone normalization uses trimmed local/`+84`/`84` forms; final shared canonicalization and validation compatibility must be agreed under V11-DES-03, not guessed from the conflicting former E.164 design. Clients cannot set role/status/permissions/tenant.

The existing request subset remains recognizable, but the full simulation/context contract is design-pending V11-DES-02:

```json
{"fullName":"Driver Example","email":"driver@example.test"}
```

Existing challenge metadata names: `registrationId`, `channel`, `expiresAt`, `resendAvailableAt`; queueing is not provider delivery confirmation. Illustrative verification result, using the existing envelope:

```json
{"success":true,"message":"Account verified. You can now sign in."}
```

The Driver then uses shared login, current account-state and required MFA gates. Registration verification never returns access/refresh tokens or an unconditional profile.

## Sequence and concurrency

```mermaid
sequenceDiagram
    actor Driver
    participant User as User and Access
    participant Delivery as Notification delivery
    Driver->>User: Register with contact and optional password
    User->>User: Persist pending identity/account; reject privilege injection
    Driver->>User: Consent and complete labelled VNeID simulation
    User->>Delivery: Queue contact verification OTP
    Driver->>User: Verify purpose-bound code/context
    User->>User: Check simulation, expiry, attempts and independent restrictions
    User-->>Driver: Verified; no normal session
    Driver->>User: Shared login primary proof then TOTP if enabled
    User-->>Driver: Necessary tokens only after all login gates
```

OTP lasts five minutes and expires at equality. Three failures lock authentication for fifteen minutes; resend waits sixty seconds, invalidates the old code and cannot reset the failure counter or bypass lock. Serialize verification/resend/activation and normalized-contact uniqueness; retry creates no extra identity/session. Duplicate pending registration uses secure recovery rather than silently overwriting the prior record or creating another account.

Simulation is labelled mock information-sharing with explicit consent, no real government verification. The [21 October V11-06 clarification](../../05-project-management/decision-log/2026-10-21-vneid-simulation-guid.md) requires a fake API to return a GUID substitute tied to the user and User & Access to persist that binding. The GUID is a mock identifier, not a government identity number or authentication credential. Failed/declined completion blocks activation. V11-DES-02 defines the fake API path/property name, other fields, repeated-call GUID behavior, evidence/failures/retention; no optional simulation exemption applies to Drivers.

## Errors and delivery

Retain safe `VALIDATION_FAILED`, `CONTACT_EXISTS`, `REGISTRATION_NOT_FOUND`, `REGISTRATION_CLOSED`, `OTP_INVALID`, `OTP_EXPIRED`, `OTP_LOCKED`, `OTP_COOLDOWN` outcomes as existing integration names. Framework validation and business envelopes require V11-DES-03 agreement; do not claim uniform RFC problem details. Invalid/expired credentials and incomplete simulation remain explicit safe errors, with final new codes pending. Failed delivery preserves the created account/challenge and records pending/failed state; retries do not roll back/recreate business records (D10).

## Retained decision references

| Existing reference | v1.1 disposition |
|---|---|
| DEC-DRV-01 | Password optional; passwordless verified-channel login retained. |
| DEC-DRV-02 | Five-minute OTP, sixty-second resend, three failures/fifteen-minute lock unchanged. |
| DEC-DRV-03 | If both supplied, email is registration channel; independent verified email or phone can subsequently support login. |
| DEC-DRV-04 | No Admin approval for Driver; simulation/contact verification activate eligibility, then separate login (D06). |

[Pending actions](../../02-architecture/api/v1.1-contracts.md#design-pending-owneraction-register): V11-DES-03/V11-DES-02. [New acceptance scenarios](../../04-testing/requirements-traceability/acceptance-scenarios.md) are NOT RUN.
