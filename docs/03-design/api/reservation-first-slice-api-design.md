# Reservation first slice — API and owner contracts

## Overview

| Metadata | Value |
|---|---|
| Owner | Parking Operations; User & Access, Facility/Gate and Payment supply authoritative facts |
| Status | PARTIAL implementation: opt-in create admission and own receipt; finalization gated. Remaining routes/contracts are proposed |
| Task | SPARK-273 / US-D01, US-D05, US-D10; implementation evidence in the [session](../../../ai/tasks/active/TASK-SPARK-273-SESSION-20261108.md) |
| Reviewed checkout | 7c47755d83cbe4eea2d2e887a2abc49e6decd203; old handoff SHA is historical |
| Requirements | [Reservation SRS](../../01-requirements/srs/modules/04-reservations-allocation.md), [BR-RES](../../01-requirements/business-rules/04-reservations-allocation.md), [UC-RES-01](../../01-requirements/use-cases/uc-res-01.md), [UC-RES-02](../../01-requirements/use-cases/uc-res-02.md), [UC-RES-03](../../01-requirements/use-cases/uc-res-03.md) |
| Approval | [D1–D4c](../../05-project-management/decision-log/2026-11-05-reservation-first-slice.md); [authorization matrix](../../01-requirements/srs/authorization-operation-classification.md), [ADR §9](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md#9-global-saga-retry-redelivery-timeout-and-unknown-outcome-policy) |
| Implementation gate | [Reviewable architecture choices](../../02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md) |

## API Specification

POST `/api/reservation-requests` and GET `/api/reservation-requests/{requestId}` are implemented as an opt-in, gated foundation; current verification remains pending. Other routes in this section are **proposed**, JSON request/response where a body exists. The proposed Driver list conflicts with the existing Operator `/api/reservations` route: route resolution requires caller review before implementing it. Use the existing `success/data` success envelope and `success/code/message/errors/traceId` error envelope; actual status is HTTP-only. Return `Cache-Control: no-store`. An own-scoped absent or foreign identifier returns indistinguishable 404. No end-user endpoint accepts an Account/User identity from client input.

| Method / URL | Freshness / authentication | Scope and purpose |
|---|---|---|
| POST `/api/reservation-terms` | TOKEN / validated Driver JWT | Review own vehicle, requested slot/interval and authoritative versioned terms; no claim/order/booking. Missing commercial config is an error, never zero price. |
| POST `/api/reservation-requests` | REQUEST / validated Driver JWT | Admit own normalized create intent and return durable receipt. Required `Idempotency-Key`. |
| GET `/api/reservation-requests/{requestId}` | TOKEN / validated Driver JWT | Observe only own receipt, including pending work without a booking/session. No body. |
| GET `/api/reservations` | TOKEN / validated Driver JWT | SQL-paginated own list; query `cursor`, `pageSize` (proposed default 20, 1–100). Stable descending created-at then ID; opaque cursor bound to same filters. No body. |
| GET `/api/reservations/{reservationId}` | TOKEN / validated Driver JWT | Own booking, accepted terms/deadlines, requested versus assigned resource, refund eligibility and independent payment/visit state. No body. |
| POST `/api/reservations/{reservationId}/cancellation-requests` | REQUEST / validated Driver JWT | Own cancellation intent, separate durable receipt; required `Idempotency-Key`. Booking is unchanged while pending authorization. |

TOKEN reads accept remaining 15-minute token staleness and still filter local ownership. REQUEST means Account-current asynchronous admission, not a synchronous UserService round trip. Use API `ICurrentActor` from validated `sub` (User), `account_id` (Account), `role`; reject malformed/missing claims. Unknown JSON fields are ignored without changing protected state.

### Request samples and fields

Review:

```json
{"vehicleId":"00000000-0000-4000-8000-000000000001","siteId":"00000000-0000-4000-8000-000000000002","requestedSlotId":"00000000-0000-4000-8000-000000000003","mode":"SPECIFIC_SLOT","start":"2026-11-10T03:00:00Z","end":"2026-11-10T04:00:00Z"}
```

Create uses the same fields plus the reviewed `termsId` and `termsVersion`:

```json
{"vehicleId":"00000000-0000-4000-8000-000000000001","siteId":"00000000-0000-4000-8000-000000000002","requestedSlotId":"00000000-0000-4000-8000-000000000003","mode":"SPECIFIC_SLOT","start":"2026-11-10T03:00:00Z","end":"2026-11-10T04:00:00Z","termsId":"00000000-0000-4000-8000-000000000004","termsVersion":"pricing-42/operations-7"}
```

| Field | Location / type | Required / semantics |
|---|---|---|
| vehicleId, siteId, requestedSlotId | Body / nonempty UUID | Required/non-null. Server resolves Account binding, CAR eligibility, tenant/site and slot membership. |
| mode | Body / enum string | Required/non-null, only SPECIFIC_SLOT for new creation. |
| start, end | Body / offset datetime | Required/non-null; normalize UTC instants; start future, positive interval and effective duration/horizon/buffer. |
| termsId, termsVersion | Create body / UUID, string | Required/non-null; server-issued review bound to normalized intent and Account. Not client-authoritative pricing. |
| Idempotency-Key | Header / proposed UUID | Required on create/cancel; scope Account + operation, bind normalized permitted intent/target. Retain durable key/fingerprint beyond transport inbox cleanup. |
| kind | Cancellation body / enum | Required: VOLUNTARY or FACILITY_FAILURE. The latter requires a currently exercisable earned right; client cannot manufacture eligibility. |
| expectedVersion | Cancellation body / positive integer | Required; conflicts appear in the receipt after current-access evaluation. |

Cancellation example: `{"kind":"FACILITY_FAILURE","expectedVersion":4}`. Omitted/null required fields fail validation. Client amounts, status, Account IDs and allocation identities are not writable, even if supplied as ignored unknown fields.

### Response samples

Create/cancel: **202 Accepted**, `Location: /api/reservation-requests/00000000-0000-4000-8000-000000000005`, only after durable admission:

```json
{"success":true,"data":{"requestId":"00000000-0000-4000-8000-000000000005","state":"PENDING_AUTHORIZATION","reservationId":null,"code":null}}
```

Own receipt: **200 OK**, same shape with state `PENDING_AUTHORIZATION`, `PREPARING`, `COMPLETED`, `DENIED`, `FAILED_DEPENDENCY`, `REJECTED` or `AWAITING_RECONCILIATION` (proposed wire names). A completed cancellation receipt describes cancellation, not successful refund payout. `reservationId` is nullable until booking exists; `code` is nullable until an outcome/reason exists. A dependency error after admission is a receipt outcome rather than retroactive failure to accept the HTTP request.

Review response: **200 OK**, `data` includes `termsId`, `termsVersion`, `validUntil`, normalized input, `vehicleBindingRevision`, `structureRevision`, effective Operations policy version, authoritative `pricingReference`, `pricingVersion`, `coverageVersion` (nullable only when no coverage), `bookedAmount`, `depositAmount`, `currency`, prepayment mode, cancellation/extra-use terms and deadline formula. Money is decimal VND under Payment's rounding contract. Actual H is calculated at hold creation and disclosed before any payment order. If elapsed review validity or a changed price/version changes accepted terms, require renewed review; no silent repricing. Exact quote issuance/expiry and version-reservation protocol remain OPEN with Payment.

Booking response: **200 OK**, own `data` contains ID/version, lifecycle, vehicle reference, site, requested slot, assigned slot/allocation ID/version (nullable), UTC interval/A/F/H, first earned eligibility time/reason (nullable), accepted pricing/coverage versions, payment/refund statuses and verified arrival/entry/departure. Requested slot never masquerades as observed vehicle location. Location carries source/time/precision or explicit unknown; no other Driver identity.

List response: `{"success":true,"data":{"items":[],"nextCursor":null}}`. Items use the booking summary above. Pagination/field names are proposed and require caller review before implementation.

### Validation and error responses

| HTTP | Code | Trigger / recovery |
|---|---|---|
| 401 | UNAUTHENTICATED | Missing/invalid bearer or required identity claims; preserve `WWW-Authenticate: Bearer`; sign in again. |
| 403 | FORBIDDEN | Valid non-Driver actor; no receipt or effect. |
| 400 | INVALID_REQUEST | Malformed JSON, invalid field/type/time/key; field errors without raw secret/input echoes. |
| 404 | RESERVATION_NOT_FOUND / REQUEST_NOT_FOUND | Absent or foreign own identifier, indistinguishable. |
| 409 | IDEMPOTENCY_KEY_REUSED | Same Account/operation/key with different normalized intent; use original receipt or a new reviewed intent. |
| 409 | TERMS_STALE / VEHICLE_CONFLICT / CAPACITY_CONFLICT | Stale review or rejected guarded business condition. After admission, represented by REJECTED receipt with code. |
| 409 | RESERVATION_VERSION_CONFLICT / CANCELLATION_NOT_ALLOWED | Reload own state; PARKING cannot be cancelled as unused. After admission, receipt outcome. |
| 409 | STRUCTURE_BUSY | Existing scoped edit hold prevents unsafe mutation; preserve legacy error meaning. |
| 503 | AUTHENTICATION_KEYS_UNAVAILABLE | Approved trusted-key dependency outcome; never validate an untrusted token. |
| 503 | PROTOTYPE_SCHEMA_NOT_READY | Preserve existing affected schema-readiness contract; initialization is not an upgrade. |
| 503 | DEPENDENCY_UNAVAILABLE | Required authoritative review dependency unavailable; no synthetic free quote/sellable slot. |
| 500 | INTERNAL_ERROR | Sanitized unexpected failure with trace ID; unknown commit requires same-key observation, not a new payment attempt. |

Example: `{"success":false,"code":"IDEMPOTENCY_KEY_REUSED","message":"The key is already bound to another request.","traceId":"example-trace"}`. MVC parsing, authentication challenge/forbid and exception producers use the shared envelope; HEAD/204 remain bodyless. Request-abort after durable commit does not undo admission.

### Authorization workflow and retry outcomes

Application handlers orchestrate repositories and UoW; validators perform input checks only. API maps narrow DTOs through AutoMapper and dispatches IMediator. Domain evaluates facts and transitions; Persistence owns locks/constraints; Infrastructure transports owner calls/messages. Do not retain forwarding workflow services or parallel feature stores.

Persist normalized intent, trusted actor, fingerprint, original admission/deadline, workflow and current authorization attempt with transactional Bus Outbox. Only read-only preparation before Allowed. Reservation-specific user amendment: one durable evaluation per workflow; at most three delivery attempts, each with a new envelope attempt ID and 5-second response timeout; wait 10 then 30 seconds; original 90-second overall deadline survives restart. Later attempts replay the original decision without rereading Account access. One transient 250-ms retry in the authorization consumer; no stacked delayed redelivery. Every attempt replays the same persisted workflow outcome once it exists. Accept only matching current attempt/actor/operation/workflow and a timely receipt; original DecidedAt may precede the current delivery attempt; fence stale/late Allowed and terminal resurrection. Denial is distinct from unavailable. JWT expiry after ingress does not itself deny Account-current; post-Allowed point-in-time revocation race remains accepted.

After Allowed, obtain external facts outside locks; under the common guard recheck authoritative local restrictions, effective versions, vehicle use, site/slot readiness and interval claims. Missing cross-owner acceptance protocol blocks finalization. Pivot is the transaction accepting booking/claim/terms/outgoing effects; afterward recover forward or use explicit cancellation/refund. Confirmation remains gated by D4c evidence. Same business effect keeps its operation ID; compensation uses another ID referencing it. For unknown effect, status checks at +0/+5/+20 seconds, 2-second attempts, 60-second reconciliation deadline; unresolved → manual review, not failed. Retain effect identity at least seven days after terminal resolution, longer for unsettled money, and durable business uniqueness for replay.

| Case | Observable outcome |
|---|---|
| Identical replay, including after deadline | Original receipt; never extend H or create another charge. |
| Different key payload | 409; original remains unchanged. |
| Denied/unavailable cancellation | Receipt outcome; booking/claims unchanged. |
| Entry versus accepted cancellation | One guarded winner; arrival alone does not decide it. |
| H expired but now<T | Compatible reacquisition may attempt confirmation; pre-commit check alone is insufficient. |
| Unconfirmed at/after T | EXPIRED; late verified funds reconcile/refund without entitlement. |
| Confirmed but unusable at T | Facility-failure cancellation; no loss of physical PARKING facts. |
| Recovery after F | Preserve earned eligibility; no payout until exercise or applicable automatic cancellation. |

## Cross-owner interfaces to agree

Wire contracts below are PROPOSED, not claims of producer availability. Use separately versioned messages; a service may not reference another service's Application assembly as a contract library.

| Boundary / owner | Required request and response | Unresolved dependency |
|---|---|---|
| Account-current / UserService | Request: workflow/request ID, attempt ID/number, operation CREATE_RESERVATION or CANCEL_OWN_RESERVATION, trusted User/Account/role, admission and deadline. Decision: all correlation fields, decision ID/time, Allowed/Denied, safe reason; evaluate current User–Account link, active states and Driver role. Durable duplicate-attempt response. | Shared DTO ownership, broker ACL/queue topology and persisted decision/reply design need approval. Existing session-current service is not a replacement for Account-current. |
| Vehicle binding / UserService | Reuse [exact binding API](vehicle-registration-api-design.md): Account, stable vehicle and expected revision → ACTIVE CAR + revision or ineligible/stale; no raw JWT persisted. | Existing shared service-key exception covers this lookup only. No removal/transfer handshake is inferred; scope new writes if a binding writer cannot participate. |
| Structure / Facility and Operations | Tenant/site/slot, active/readiness, vehicle compatibility, resource/config version, zone/fallback/backup and coordinates; absent distinct from unavailable. | Extend the existing structure prepare/complete guard to every competing closure/retirement/config writer. A remote read alone does not fence later changes. |
| Quote/coverage / Payment and Operations | Quote ID/version, bound Account/vehicle/site/slot/interval, validity, currency, booked/deposit values, FULL/PERCENTAGE/FIXED, rounding, coverage references/version, immutable terms. | Current parking-fee calculation is not an accepted reservation quote. Define issuance/consumption and coverage-cancellation handshake before sale. |
| Deposit order / Payment | Stable operation ID, reservation/tenant/site/Account, RESERVATION_FEE, exact H, money/currency, accepted terms version. Identical replay returns same order; mismatched payload conflicts. | Provider deadline support cannot extend Operations eligibility; Payment owner must supply actual order/status adapter. |
| Verified money/refund / Payment | Authenticated financial event ID and transaction/order/operation/reservation scope, amount/currency/terms version and verified state; query by operation ID with Succeeded/RejectedNoEffect/Processing/NotFound/Unknown. Refund has its own ID, cause, original effect and eligible remaining value. | Outcome transport authentication and reconciliation/refund ownership/retention must be agreed; NotFound alone is inconclusive. |
| Gate / Operations | In-process mediated assignment read/admission: reservation, vehicle, site, usable allocation ID/slot/version; authoritative event ID/source and occurred/received time distinguish arrival, entry, departure. | Gate owner must supply trusted evidence; no preferred-slot fallback or payment-only occupancy release. |
| Private map / Operations | Own pending booking/receipt plus requested versus assigned resource, location precision/evidence; explicit unknown and unavailable. | Public layout remains sanitized; never infer a session location from preference. |

## Review checks and deferred items

- [x] D1–D4c mapped to input, time, retry and ownership behavior; old cap removed.
- [x] Proposed routes and fields distinguished from implemented ones.
- [x] Current authentication and owner dependencies inspected; missing producer behavior disclosed.
- [x] Authentication cache parameters and message trust/ownership approved; later amendment requires service-local code and once-per-workflow authorization.
- [ ] Agree versioned commercial/resource contracts and D4c commit evidence.
- [ ] Implement host → mediator → validation → handler → repositories/UoW → outbox and all competing writers.
- [ ] Run focused PostgreSQL, broker and live owner checks; no such results claimed here.
- [ ] Driver switching/rescheduling, pooled/motorcycle booking and new monthly/yearly workflows remain deferred.

## Current runtime boundary

Admission performs shape/identity validation and durably records the intent and Account-current request in one UoW transaction. It does not yet resolve verified vehicle ownership, Facility facts or commercial terms. A timely Allowed result currently closes the receipt as `FAILED_DEPENDENCY` / `RESERVATION_FINALIZATION_NOT_READY`; it creates no booking, slot claim, order or refund. A denial closes as `DENIED`; bounded timeout closes as `FAILED_DEPENDENCY` / `AUTHORIZATION_UNAVAILABLE`. No other proposed terminal lifecycle is emitted by this foundation. `reservationId` remains null.

Policy resolution reads versioned platform/site data. Missing configuration or a missing explicit site late-tolerance choice fails closed; no C# business defaults substitute for database rows. Accepted-value snapshot storage is declared but is not yet written by a live finalization path. D4c remains a separate gate, including zero-price bookings.

Legacy Gate handlers now orchestrate repositories/UoW directly. They do not treat requested slot preference as observed location or as a usable allocation. Existing operator trust/plate behavior remains legacy; stable verified-vehicle exclusion across sites and the live Facility usability handshake remain pending integration and must not be inferred from these handlers.

Gate entry/departure emits `OperationsGateAuditV1` through the same scoped PostgreSQL outbox transaction. With `Messaging:AuditEnabled=false`, effectful Gate writes fail with 503 `AUDIT_PUBLISHING_UNAVAILABLE`; an already completed departure replay creates no new effect/event. Broker outage with configured capture preserves outbox work. Audit consumer/retention belongs to the separate service; see the [deployment checklist](../../04-testing/reports/SPARK-273-deployment-checklist.md).

## Service-local contract packaging

Operations owns its authentication implementation and local Account-current/audit wire DTOs. UserService has independently compiled compatible Account-current DTOs. No src/Shared project or cross-service assembly reference is required. Existing message namespace/type identity is retained for transport compatibility; the wire contract, not a shared source file, is the service boundary.
