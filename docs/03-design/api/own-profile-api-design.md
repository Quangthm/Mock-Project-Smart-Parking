# Own normal profile — API Design

| Metadata | Value |
| --- | --- |
| Status | APPROVED contract decisions D1–D4; implementation and scoped verification complete; manual schema deployment required |
| Contract version / date | v1 / 2026-10-26 |
| Owning service / author | User & Access / SPARK-248 implementation |
| Jira design / implementation / verification | US-U01 / SPARK-248 |
| Requirements baseline | v1.1; initial develop `ad0a3351f2ad7f48610bd3b85d7913fb68882838`; latest remote/local develop `7d9d39205ec8659f28fdccaa0817f08541e382f7` verified 2026-10-26 (identity/API/event baseline unchanged) |
| Implementation | Uncommitted work on `feat/SPARK-248-manage-own-profile`; execution evidence in the task record |

## Overview

All four roles may read their own profile and change their shared User name through the exact authenticated Account/session. Avatar replacement/removal, contact verification, password and 2FA changes are deferred.

| Traceability | Reference |
| --- | --- |
| FR-AUTH-03, FR-AUTH-06 / US-U01 | [Identity requirements](../../01-requirements/srs/modules/01-identity-access.md) |
| Permission / ownership | [Service ownership](../../02-architecture/service-boundaries/ownership-and-events.md) |
| Existing routes/envelope | [v1.1 contracts](../../02-architecture/api/v1.1-contracts.md#route-and-schema-authority) |
| Media | [Media design](../../02-architecture/service-boundaries/media-contract.md); approved scope here preserves the existing stored avatar representation |
| Approval evidence | User's 24 October answers, recorded in [task decisions](../../../ai/tasks/completed/TASK-SPARK-248-SESSION-20261024.md#4-technical-decisions) |
| Implementation protocol | [Code-flow and authorization review](../../04-testing/own-profile-review.md) |
| Schema/event delivery | [Outbox deployment and verification](../../04-testing/own-profile-outbox.md) |

D1 approves the routes, additive `avatarUrl` response and framework error codes. D2 approves Unicode scalar counting. D3 approves immutable account-bound sessions, stable account selection and role-only authorization. D4 approves the changed-only event and EF Bus Outbox. These are explicit user decisions, not inferred from the former uncommitted implementation.

## API Specification

### Read own profile

| Item | Contract |
| --- | --- |
| Method / URL | `GET /api/auth/me` |
| Availability | Implemented in working tree; database deployment and verification are separate gates |
| Authentication | Current normal account-bound Bearer session |
| Permission / account state | Driver, Operator, Owner or Admin; exact Account and User active, session current, role still matches |
| Resource scope | Shared User resolved from authenticated Account |
| Content type | Response: `application/json` |
| Headers | `Authorization: Bearer <access-token>`; response `Cache-Control: no-store` |

Request: **No body**. There is no route/body AccountId or UserId selector.

Response: HTTP 200, with the shared response sample below.

### Change own name

| Item | Contract |
| --- | --- |
| Method / URL | `PATCH /api/auth/me` |
| Availability | Implemented in working tree; required session/outbox schema must be deployed |
| Authentication | Current normal account-bound Bearer session |
| Permission / account state | Same four roles; exact Account, User, role and session revalidated within the coordinated save |
| Resource scope | User resolved from authenticated Account; name is shared by that User's other Accounts |
| Content type | Request and response: `application/json` |
| Headers | `Authorization: Bearer <access-token>`; response `Cache-Control: no-store`; no ETag required |

### Request sample

```json
{"fullName":"  Nguyễn  An  "}
```

| Field | Location | Type | Required / nullable | Constraints and meaning |
| --- | --- | --- | --- | --- |
| fullName | body | string | Required / non-null | Nonblank; trim surrounding whitespace; preserve internal spacing and Unicode; reject control characters and invalid UTF-16; maximum 255 Unicode scalar values after trim |

Omitted, null, blank and invalid names produce 400. Supplementary characters count once; combining marks count separately. There is no Unicode normalization or grapheme/display-character count. The checked PostgreSQL column remains `full_name VARCHAR(255)`; no name-capacity schema change is made.

Unknown JSON properties are ignored. They cannot change avatar, contacts, credentials, role, status, permissions or target identity. A valid name plus unsupported fields succeeds only for the name. An unsupported field alone does not supply the required name.

### Shared response sample

Both GET and PATCH return HTTP 200:

```json
{
  "success": true,
  "data": {
    "userId": "11111111-1111-1111-1111-111111111111",
    "fullName": "Nguyễn  An",
    "email": "person@example.test",
    "role": "driver",
    "avatarUrl": null
  }
}
```

| Field | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| success | boolean | No | Successful supported operation |
| data.userId | UUID | No | Affected shared User, resolved through authenticated Account |
| data.fullName | string | No | Persisted name |
| data.email | string | No | Existing read-only contact; empty string if no stored email, preserving the earlier /me convention |
| data.role | string | No | Exact authorized Account's current mapped role: driver/operator/owner/admin |
| data.avatarUrl | string | Yes | Stored legacy value unchanged; no URL-to-asset conversion, ownership assertion or client write permission |

There is no `{ reference }` avatar shape, upload/delete success, or profile alias under `/api/profile/me`. Role and email are read-only response facts.

### Account/session compatibility

New sessions select the eligible Account with the lowest Account ID under the implementation's stable GUID ordering. The selected Account ID is stored immutably and signed into `account_id`; `sub` remains User ID and `jti` identifies the exact access token. Refresh retains the Account and original refresh expiry. Account switching is deferred. Legacy sessions lacking binding cannot authenticate through the updated Bearer path or refresh; sign in again. No account association is guessed or backfilled.

Authorization uses current roles, not `Accounts.Permissions`. The latter remains outside this slice for future removal. An active second Account cannot authorize a request through a locked/revoked first Account. Token role claims alone grant nothing; authoritative current roles must match.

### Validation and error responses

Failures retain `success:false`, `code`, `message`, and optional `errors:[{field,message}]`. Actual HTTP status is set in HTTP and is absent from JSON.

```json
{"success":false,"code":"VALIDATION_FAILED","message":"Validation failed.","errors":[{"field":"FullName","message":"Enter a display name of 1 to 255 characters without control characters."}]}
```

The validator supplies the fixed name message above; “characters” uses the approved Unicode scalar counting rule. Binding errors use generic text and do not echo JSON values or internal type names.

| HTTP | Code | Trigger | Client recovery |
| --- | --- | --- | --- |
| 400 | VALIDATION_FAILED | Malformed JSON, wrong input type, missing/invalid name | Correct request |
| 401 | INVALID_TOKEN | Missing, invalid, expired, revoked, unbound or currently ineligible Bearer session | Sign in again; `WWW-Authenticate: Bearer` is preserved |
| 403 | FORBIDDEN | Account/User/role loses eligibility after initial Bearer validation but before the guarded operation; other authorization forbids | Do not retry unchanged credentials as if access were granted |
| 404 | NOT_FOUND | Unknown route, including deferred avatar mutation route | Use documented routes |
| 405 | METHOD_NOT_ALLOWED | Unsupported method | Use documented method |
| 409 | PROFILE_ACCESS_CONFLICT | Bounded authorization-lock contention/deadlock/serialization failure before a successful commit | Retry with current credentials; this is not a name-version conflict |
| 415 | UNSUPPORTED_MEDIA_TYPE | Unsupported request media type | Send JSON |
| 429 | TOO_MANY_REQUESTS | Existing configured rate limiter rejects a request | Observe limiter policy; no new profile limiter is added |
| 500 | INTERNAL_ERROR | Unexpected failure, including missing schema or database failure | Investigate using server correlation; response exposes no internal exception |

The initial Bearer gate returns 401 if it already observes an ineligible account; a later guarded account-policy rejection returns 403. Cancellation caused by the request abort is not translated to generic 500. A started response failure propagates rather than appending JSON. HEAD errors have no body; success file/image/streaming/204 results are not wrapped.

### Integration event

User & Access owns `UserProfileNameChanged` v1; Notification & Audit is the documented eventual projection owner. This task implements no consumer.

| Payload field | Type | Meaning |
| --- | --- | --- |
| eventId | UUID | Assigned once; also the transport MessageId; stable across relay retry |
| schemaVersion | integer | 1 |
| actorAccountId | UUID | Requesting Account, distinct from affected User |
| affectedUserId | UUID | Shared User whose name changed |
| occurredAtUtc | timestamp | Server UTC event time |
| correlationId | string | Server request/trace correlation |

No name, contact, password, token or raw session identifier is included. Emit only when the normalized name differs from the current persisted name. A no-op returns the current profile without an event. Durable outbox recording is part of the name transaction.

### Retry and state outcomes

| Case | Observable contract |
| --- | --- |
| Competing authorized name changes | Last committed write wins; no client version or mandatory ETag |
| Revocation/lock wins coordinated ordering | Reject name write; no outgoing event |
| Authorized save commits first | Subsequent revocation takes effect afterwards; no retroactive cancellation |
| Successful changed name | Name and event durably recorded together; Audit processing may still be pending |
| Broker unavailable or delivery disabled | Capture remains durable in PostgreSQL; later relay delivery is asynchronous |
| Normalized no-op | Return current profile; no new event |
| Timeout/unknown commit outcome | No blind automatic write retry; caller may read current profile; no request-idempotency guarantee is introduced |
| Required outbox write cannot persist | Roll back name and event; no direct-publish/drop/fake-success fallback |
| Session expiry equality | Expiry at or before server time is invalid |

See the [implementation review](../../04-testing/own-profile-review.md) for the separately justified lock protocol and [outbox guide](../../04-testing/own-profile-outbox.md) for manual schema deployment and durable routing prerequisites.

## Open questions and deferred items

| Item | Decision or implementation gap | Affected operation | Owner / Jira | Approval reference |
| --- | --- | --- | --- | --- |
| Verification complete | Build, HTTP integration, real PostgreSQL races and RabbitMQ outage/recovery passed; commands and results are recorded in the task record | All | SPARK-248 | Task record |
| Manual deployment | Apply reviewed session and framework outbox scripts to the selected User database; provision durable queue before enabling delivery | Runtime deployment | User/service maintainer | Approved scripts-only scope |
| Deferred | Avatar replacement/removal, verified contact/password/2FA changes, account switching, Audit consumer | Separate slices | Respective owners | User brief and D1–D4 |
| Existing authentication alignment | Legacy token lifetime/login payload differences from v1.1 remain separate security work; this slice does not claim that all auth requirements are met | Login/refresh | User/security | See review constraints |

## Review checks

- [x] D1–D4 approval and requirements/ownership linked.
- [x] Routes, fields, nullability, examples and unknown-field behavior specified.
- [x] Exact Account/session scope and role-only policy explicit.
- [x] Name counting, no-op event and retry/concurrency outcomes specified.
- [x] Manual schema/topology deployment and deferred work distinguished.
- [x] Implementation and real PostgreSQL/broker checks passed; see task record. Human code review remains separate.
