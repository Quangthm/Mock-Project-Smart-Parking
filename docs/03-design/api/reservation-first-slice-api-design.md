# Reservation first slice — API and owner contracts

## Overview

| Metadata | Value |
|---|---|
| Owner | Parking Operations; User & Access, Facility/Gate and Payment supply authoritative facts |
| Status | WORK IN PROGRESS: admission, preview/review, own reads and evaluation-checkpoint confirmation in source; missing owner/capacity integration prevents live booking. Current build/runtime UNVERIFIED |
| Task | SPARK-273 / US-D01, US-D05, US-D10; implementation evidence in the [session](../../../ai/tasks/active/TASK-SPARK-273-SESSION-20261108.md) |
| Reviewed checkout | 7c47755d83cbe4eea2d2e887a2abc49e6decd203; old handoff SHA is historical |
| Requirements | [Reservation SRS](../../01-requirements/srs/modules/04-reservations-allocation.md), [BR-RES](../../01-requirements/business-rules/04-reservations-allocation.md), [UC-RES-01](../../01-requirements/use-cases/uc-res-01.md), [UC-RES-02](../../01-requirements/use-cases/uc-res-02.md), [UC-RES-03](../../01-requirements/use-cases/uc-res-03.md) |
| Approval | [D1–D4c](../../05-project-management/decision-log/2026-11-05-reservation-first-slice.md); [authorization matrix](../../01-requirements/srs/authorization-operation-classification.md), [ADR §9](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md#9-global-saga-retry-redelivery-timeout-and-unknown-outcome-policy) |
| Implementation gate | [Reviewable architecture choices](../../02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md) |

## API Specification

The table distinguishes current source routes from proposed cancellation work. All current Driver routes require admission enabled and validated Driver JWTs. Commercial/Facility producers and Operations capacity checks are not integrated; preview/review currently fail closed, not fake a price or valid booking. Use `success/data` success and `success/code/message/errors/traceId` error envelopes; status is HTTP-only. Return `Cache-Control: no-store`. Absent/foreign own IDs return indistinguishable 404. No end-user endpoint accepts an Account/User identity as authority. [9 November decisions](../../05-project-management/decision-log/2026-11-09-reservation-confirmation-and-arrival.md) supersede older identity/cutoff proposals; [producer handoff](../../05-project-management/handoffs/2026-11-09-reservation-producer-handoff.md) lists current integration gates.

| Method / URL | Freshness / authentication | Scope and purpose |
|---|---|---|
| POST `/api/reservation-price-previews` | TOKEN / validated Driver JWT | Current source: server calculation while editing slot/time; optional vehicle/method; no claim/order/booking. |
| POST `/api/reservation-terms` | TOKEN / validated Driver JWT | Review own vehicle, requested slot/interval and authoritative versioned terms; no claim/order/booking. Missing commercial config is an error, never zero price. |
| POST `/api/reservation-requests` | REQUEST / validated Driver JWT | Admit own normalized create intent and return durable receipt. Required `Idempotency-Key`. |
| GET `/api/reservation-requests/{requestId}` | TOKEN / validated Driver JWT | Observe only own receipt, including pending work without a booking/session. No body. |
| GET `/api/me/reservations` | TOKEN / validated Driver JWT | Current source: own list, pageSize 20 (1–100), optional `before` + `beforeId` together; descending createdAt then ID. Use last item's createdAt/ID for next page. |
| GET `/api/me/reservations/{reservationId}` | TOKEN / validated Driver JWT | Current source: own booking summary; Operator `/api/reservations` remains separate. |
| POST `/api/me/reservations/{reservationId}/cancellation-requests` | REQUEST / validated Driver JWT | PROPOSED future phase only: separate receipt, required Idempotency-Key; no cancellation route implemented here. |

TOKEN reads accept remaining 15-minute token staleness and still filter local ownership. REQUEST means Account-current asynchronous admission, not a synchronous UserService round trip. Use API `ICurrentActor` from validated `sub` (User), `account_id` (Account), `role`; reject malformed/missing claims. Unknown JSON fields are ignored without changing protected state.

### Request samples and fields

Review:

```json
{"vehicleId":"00000000-0000-4000-8000-000000000001","siteId":"00000000-0000-4000-8000-000000000002","requestedSlotId":"00000000-0000-4000-8000-000000000003","start":"2026-11-10T03:00:00Z","end":"2026-11-10T04:00:00Z","paymentMethod":"PAYOS"}
```

Create uses the same fields plus the reviewed `termsId` and `termsVersion`:

```json
{"vehicleId":"00000000-0000-4000-8000-000000000001","siteId":"00000000-0000-4000-8000-000000000002","requestedSlotId":"00000000-0000-4000-8000-000000000003","mode":"SPECIFIC_SLOT","start":"2026-11-10T03:00:00Z","end":"2026-11-10T04:00:00Z","paymentMethod":"PAYOS","termsId":"00000000-0000-4000-8000-000000000004","termsVersion":"owner-issued-version"}
```

| Field | Location / type | Required / semantics |
|---|---|---|
| vehicleId, siteId, requestedSlotId | Body / nonempty UUID | Required/non-null. Server resolves Account binding, CAR eligibility, tenant/site and slot membership. |
| mode | Body / enum string | Required/non-null, only SPECIFIC_SLOT for new creation. |
| start, end | Body / offset datetime | Required/non-null; normalize UTC instants; start future, positive interval and effective duration/horizon/buffer. |
| termsId, termsVersion | Create body / UUID, string | Required/non-null; server-issued review bound to normalized intent and Account. Not client-authoritative pricing. |
| paymentMethod | Review/create body / string | Required, max 32; producer validates supported method and binds it into accepted quote. PAYOS above is illustrative, not proof of a configured provider. |
| Idempotency-Key | Header / proposed UUID | Required on create/cancel; scope Account + operation, bind normalized permitted intent/target. Retain durable key/fingerprint beyond transport inbox cleanup. |
| kind | Cancellation body / enum | Required: VOLUNTARY or FACILITY_FAILURE. The latter requires a currently exercisable earned right; client cannot manufacture eligibility. |
| expectedVersion | Cancellation body / positive integer | Required; conflicts appear in the receipt after current-access evaluation. |

Cancellation example: `{"kind":"FACILITY_FAILURE","expectedVersion":4}`. Omitted/null required fields fail validation. Client amounts, status, Account IDs and allocation identities are not writable, even if supplied as ignored unknown fields.

### Response samples

Create/cancel: **202 Accepted**, `Location: /api/reservation-requests/00000000-0000-4000-8000-000000000005`, only after durable admission:

```json
{"success":true,"data":{"requestId":"00000000-0000-4000-8000-000000000005","state":"PENDING_AUTHORIZATION","reservationId":null,"code":null}}
```

Own receipt: **200 OK**, same shape with current source states `PENDING_AUTHORIZATION`, `PREPARING`, `BOOKING`, `COMPLETED`, `EXPIRED`, `DENIED`, `FAILED_DEPENDENCY` or `REJECTED`. `reservationId` is null until booking exists. `BOOKING` is not confirmation. `COMPLETED` for create means verified confirmation; `EXPIRED` with reconciliation acceptance does not mean a refund paid out. Transient dependency codes can be present while work remains pending. Cancellation outcomes remain future work.

Preview response: **200 OK**, `data` has `bookedAmount`, nullable `depositAmount`, `currency`, `pricingVersion`, `requiresCompleteSelection`. Site/slot/start/end are required; vehicle/method may be null before form completion. A preview is not accepted terms or a hold.

Review response: **200 OK**, `data` has `termsId`, `termsVersion`, `validUntil`, `commercial` and `operationalPolicy`. Commercial contains quoteId/version/validUntil, normalized selection, authenticated Account, bookedAmount/depositAmount/currency, prepaymentMode, coverageVersion and disclosure. OperationalPolicy contains platform/site revision IDs/versions, effective values (minutes except COUNT/LIMIT), late-tolerance enablement and cancellation timezone. Binding is deliberately not evaluated during review; it is recorded once after workflow authorization. Actual H is calculated when the hold is created. Expired/changed terms require renewed review. Missing producer transport remains an explicit unavailable error.

Booking response: **200 OK**, current `data` fields: reservationId, state, siteId, nullable requestedSlotId, start/end, nullable holdExpiresAt/orderId, bookedAmount/depositAmount/currency, nullable confirmedAt, createdAt. `confirmedAt` records the accepted evaluation time; committed CONFIRMED is required for entitlement. There is no CONFIRMATION_PENDING stage. RequestedSlotId is a preference, not an observed or allocated location. Allocation, refund, cancellation and visit detail fields are future phase work; do not infer them from this summary.

List response: `{"success":true,"data":[]}`. Items use the current booking summary. The current implementation returns an array, not an items/nextCursor object. For the next page send both the last item's createdAt as `before` and reservationId as `beforeId`.

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
| 503 | OWNER_UNAVAILABLE / OWNER_CONTRACT_INVALID | Required owner absent or its response violates the bound contract; no synthetic free quote/sellable slot. |
| 503 | RESERVATION_POLICY_UNAVAILABLE | Missing/ambiguous database policy or missing explicit site late-tolerance choice. |
| 503 | CAPACITY_COORDINATION_UNAVAILABLE | Capacity implementation unavailable; current workflow defers safely. |
| 500 | INTERNAL_ERROR | Sanitized unexpected failure with trace ID; unknown commit requires same-key observation, not a new payment attempt. |

Example: `{"success":false,"code":"IDEMPOTENCY_KEY_REUSED","message":"The key is already bound to another request.","traceId":"example-trace"}`. MVC parsing, authentication challenge/forbid and exception producers use the shared envelope; HEAD/204 remain bodyless. Request-abort after durable commit does not undo admission.

### Authorization workflow and retry outcomes

Application handlers orchestrate repositories and UoW; validators perform input checks only. API maps narrow DTOs through AutoMapper and dispatches IMediator. Domain evaluates facts and transitions; Persistence owns locks/constraints; Infrastructure transports owner calls/messages. Do not retain forwarding workflow services or parallel feature stores.

Persist normalized intent, trusted actor, fingerprint, original admission/deadline, workflow and current authorization attempt with transactional Bus Outbox. Only read-only preparation before Allowed. Reservation-specific user amendment: one durable evaluation per workflow; at most three delivery attempts, each with a new envelope attempt ID and 5-second response timeout; wait 10 then 30 seconds; original 90-second overall deadline survives restart. Later attempts replay the original decision without rereading Account access. One transient 250-ms retry in the authorization consumer; no stacked delayed redelivery. Every attempt replays the same persisted workflow outcome once it exists. Accept only matching current attempt/actor/operation/workflow and a timely receipt; original DecidedAt may precede the current delivery attempt; fence stale/late Allowed and terminal resurrection. Denial is distinct from unavailable. JWT expiry after ingress does not itself deny Account-current; post-Allowed point-in-time revocation race remains accepted.

Required completion behavior: after Allowed, obtain external facts outside locks; under the common guard recheck local restrictions, effective versions, plate use, site/slot readiness and interval claims. Missing cross-owner acceptance protocol blocks finalization. The draft still needs external-call staging and bounded unknown-effect reconciliation; its current one-second sweep is not evidence that the full ADR reconciliation schedule/retention is implemented. Preserve stable effect identity and unknown outcomes. Confirmation uses one database timestamp at the final guarded checkpoint, with durable commit before exposure. See the producer handoff for explicit unfinished Operations work.

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
| Account-current / UserService | CREATE_RESERVATION request/decision with workflow and delivery attempt identity, actor and deadlines; one durable workflow evaluation replayed in later envelopes. | Service-local DTOs and restricted broker queues are approved and implemented in the foundation; deployment proof remains required. Cancellation is future work. |
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
- [x] D4c evaluation-checkpoint cutoff approved on 10 November; deployment verification remains pending.
- [ ] Deliver versioned commercial/resource producers and complete local capacity/reconciliation.
- [ ] Implement host → mediator → validation → handler → repositories/UoW → outbox and all competing writers.
- [ ] Run focused PostgreSQL, broker and live owner checks; no such results claimed here.
- [ ] Driver switching/rescheduling, pooled/motorcycle booking and new monthly/yearly workflows remain deferred.

## Current runtime boundary

Admission records normalized intent and Account-current request in one UoW transaction. A timely Allowed result now enters PREPARING; progression persists the User binding result once, including canonical plate. Missing commercial/Facility integrations and the unavailable capacity implementation prevent a valid live booking. Invalid/expired terms or preparation deadline produce rejection. Denial remains DENIED; authorization timeout remains FAILED_DEPENDENCY / AUTHORIZATION_UNAVAILABLE. This replaces the first-slice deliberate FINALIZATION_NOT_READY terminal result. New source is not build/runtime verified.

Policy resolution reads versioned platform/site data. Missing configuration or an explicit site late-tolerance choice fails closed; no C# business defaults substitute for rows. Draft review/acceptance persists snapshots. Evaluation-checkpoint confirmation source exists, but no full live finalization path has been demonstrated, including zero-price bookings.

Gate handlers orchestrate repositories/UoW directly and acquire canonical plate before site guards. Active physical plate lookup is global across sites; a future reservation alone does not prohibit walk-in. They do not treat preference as observed location or usable allocation. Full resource/capacity coordination and the live Facility usability handshake still require integration and deployment proof.

Gate entry/departure emits `OperationsGateAuditV1` through the same scoped PostgreSQL outbox transaction. With `Messaging:AuditEnabled=false`, effectful Gate writes fail with 503 `AUDIT_PUBLISHING_UNAVAILABLE`; an already completed departure replay creates no new effect/event. Broker outage with configured capture preserves outbox work. Audit consumer/retention belongs to the separate service; see the [deployment checklist](../../04-testing/reports/SPARK-273-deployment-checklist.md).

## Service-local contract packaging

Operations owns its authentication implementation and local Account-current/audit wire DTOs. UserService has independently compiled compatible Account-current DTOs. No src/Shared project or cross-service assembly reference is required. Existing message namespace/type identity is retained for transport compatibility; the wire contract, not a shared source file, is the service boundary.
