# Reservation first slice — API and owner contracts

> **SPARK-273 architecture alignment (11 November 2026).** This document includes historical implementation notes. For the current approved workflow and confirmation boundary, see the [11 November decision](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md). Existing Facility confirmation checks remain required until the aggregate-only fast-path invariant gate is verified.

## Overview

| Metadata | Value |
|---|---|
| Owner | Parking Operations; User & Access, Facility/Gate and Payment supply authoritative facts |
| Status | Operations source baseline: version-2 `HOLD_CREATED`, hold outbox consumer, order/zero-price provisioning, verified-collection consumer and guarded paid/zero-price confirmation are implemented and registered. Payment's real commercial adapter/producer and live cross-service acceptance remain blocked/unverified. |
| Task | SPARK-273 / US-D01, US-D05, US-D10; implementation evidence in the [session](../../../ai/tasks/active/TASK-SPARK-273-SESSION-20261108.md) |
| Reviewed checkout | 7c47755d83cbe4eea2d2e887a2abc49e6decd203; old handoff SHA is historical |
| Requirements | [Reservation SRS](../../01-requirements/srs/modules/04-reservations-allocation.md), [BR-RES](../../01-requirements/business-rules/04-reservations-allocation.md), [UC-RES-01](../../01-requirements/use-cases/uc-res-01.md), [UC-RES-02](../../01-requirements/use-cases/uc-res-02.md), [UC-RES-03](../../01-requirements/use-cases/uc-res-03.md) |
| Approval | [D1–D4c](../../05-project-management/decision-log/2026-11-05-reservation-first-slice.md); [authorization matrix](../../01-requirements/srs/authorization-operation-classification.md), [ADR §9](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md#9-global-saga-retry-redelivery-timeout-and-unknown-outcome-policy) |
| Implementation gate | [Reviewable architecture choices](../../02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md) |

## API Specification

The table distinguishes current source routes from proposed cancellation work. All current Driver routes require admission enabled and validated Driver JWTs. Facility producer/transport and Operations capacity/quota/restriction checks are implemented in the unverified Commit 1 draft; the commercial owner remains unavailable, so preview/review currently fail closed, not fake a price or valid booking. Use `success/data` success and `success/code/message/errors/traceId` error envelopes; status is HTTP-only. Return `Cache-Control: no-store`. Absent/foreign own IDs return indistinguishable 404. No end-user endpoint accepts an Account/User identity as authority. [9 November decisions](../../05-project-management/decision-log/2026-11-09-reservation-confirmation-and-arrival.md) supersede older identity/cutoff proposals; [producer handoff](../../05-project-management/handoffs/2026-11-09-reservation-producer-handoff.md) lists current integration gates.

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

Create: **202 Accepted**, `Location: /api/reservation-requests/00000000-0000-4000-8000-000000000005`, only after durable admission:

```json
{"success":true,"data":{"requestId":"00000000-0000-4000-8000-000000000005","state":"PENDING_AUTHORIZATION","reservationId":null,"code":null,"contractVersion":2}}
```

Own receipt: **200 OK**, same shape with `contractVersion: 2` and current states `PENDING_AUTHORIZATION`, `PREPARING`, `HOLD_CREATED`, `DENIED`, `FAILED_DEPENDENCY` or `REJECTED`. `reservationId` is null until a hold exists. `HOLD_CREATED` means the request workflow committed its hold; the reservation remains `PENDING_PAYMENT`. Payment outcome, confirmation and reconciliation are separate reservation/payment states, never later mutations of this successful creation receipt.

Preview response: **200 OK**, `data` has `bookedAmount`, nullable `depositAmount`, `currency`, `pricingVersion`, `requiresCompleteSelection`. Site/slot/start/end are required; vehicle/method may be null before form completion. A preview is not accepted terms or a hold.

Review response: **200 OK**, `data` has `termsId`, `termsVersion`, `validUntil`, `commercial` and `operationalPolicy`. Commercial contains quoteId/version/validUntil, normalized selection, authenticated Account, bookedAmount/depositAmount/currency, prepaymentMode, coverageVersion and disclosure. OperationalPolicy contains platform/site revision IDs/versions, effective values (minutes except COUNT/LIMIT), late-tolerance enablement and cancellation timezone. Binding is deliberately not evaluated during review; it is recorded once after workflow authorization. Actual H is calculated when the hold is created. Expired/changed terms require renewed review. Missing producer transport remains an explicit unavailable error.

Booking response: **200 OK**, current `data` fields: reservationId, state, siteId, nullable requestedSlotId, start/end, nullable `holdExpiresAt`/`orderId`, `orderProvisioningState`, nullable `orderProvisioningCode`, `zeroPriceAcceptanceState`, bookedAmount/depositAmount/currency, nullable confirmedAt, createdAt. `confirmedAt` records the accepted evaluation time; committed `CONFIRMED` is required for entitlement. RequestedSlotId is a preference, not an observed or allocated location. Allocation, refund, cancellation and visit detail fields are future phase work; do not infer them from this summary.

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
| 409 | SLOT_TIME_CONFLICT / CAPACITY_UNAVAILABLE / RESERVATION_QUOTA_EXCEEDED / VEHICLE_TIME_CONFLICT / BOOKING_RESTRICTED | Selected slot, compatible capacity, quota, canonical plate or cancellation restriction rejects acceptance. Admission reports the code through its receipt. |
| 503 | CAPACITY_CONFIGURATION_REQUIRED / OCCUPANCY_RECONCILIATION_REQUIRED / CANCELLATION_HISTORY_RECONCILIATION_REQUIRED | Explicit quota or reliable retained history is missing; fix/reconcile deployment data, never guess or bypass. |
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

Account and Facility contracts are implemented drafts; Payment contracts below remain delivery requirements, not claims of available producers. Use separately versioned messages; a service may not reference another service's Application assembly as a contract library.

| Boundary / owner | Required request and response | Unresolved dependency |
|---|---|---|
| Account-current / UserService | CREATE_RESERVATION request/decision with workflow and delivery attempt identity, actor and deadlines; one durable workflow evaluation replayed in later envelopes. | Service-local DTOs and restricted broker queues are approved and implemented in the foundation; deployment proof remains required. Cancellation is future work. |
| Vehicle binding / UserService | Reuse [exact binding API](vehicle-registration-api-design.md): Account, stable vehicle and expected revision → ACTIVE CAR + revision or ineligible/stale; no raw JWT persisted. | Existing shared service-key exception covers this lookup only. No removal/transfer handshake is inferred; scope new writes if a binding writer cannot participate. |
| Structure / Facility and Operations | Tenant/site/slot, active/readiness, vehicle compatibility, resource/config version, zone/fallback/backup and coordinates; absent distinct from unavailable. | The reverse [durable dependency v1](reservation-facility-dependency-api-design.md) and dedicated Operations JWT are implemented. Verify all participating writers and recovery in deployment; a remote read alone does not fence later changes. |
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

## 11 November 2026 addendum — short creation receipt and event-driven confirmation

**Source and precedence:** [approved architectural decision](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md), [team integration contract](../../05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md). The short creation receipt, Operations hold consumer, order/zero-price provisioning, verified-collection consumer and guarded paid/zero-price confirmation are present in local source and registered. The Payment commercial adapter/producer and runtime cross-service verification remain unavailable. Existing November 10 single guarded `evaluatedAt < T` confirmation cutoff stays in force.

### Revised request and reservation state meanings (target)

`POST /api/reservation-requests` continues to accept the reviewed exact selection (`termsId`, `termsVersion`, chosen payment method and stable `Idempotency-Key`), returning `202` with a durable request receipt. `202` and `PENDING_AUTHORIZATION` do not create a hold. The receipt contract now returns `contractVersion: 2`; after authorized guarded admission, it becomes terminal `HOLD_CREATED` in the same UoW that commits the `PENDING_PAYMENT` reservation, admission dependency outcome and hold/outbox fact. The creation worker no longer processes this receipt while payment is pending. The persistent reservation remains `PENDING_PAYMENT` until a separate guarded confirmation UoW commits. Booking lifecycle `COMPLETED` remains post-parking.

`GET /api/reservation-requests/{requestId}` remains account-scoped receipt/replay; `GET /api/me/reservations/{reservationId}` remains the booking entitlement authority. Its nullable `orderId` becomes available after order provisioning returns an authoritative Payment order ID. The stable operation ID is the original request ID; accepted quote snapshot, retry state and exact H are persisted, and owner calls run outside guarded Operations transactions. `UNKNOWN` retries reuse the same operation ID; `CLOSED_NO_ORDER` and stale reviewed terms release the unpaid hold. The registered commercial adapter is currently unavailable and fails closed. For zero-price terms, the separate no-order acceptance port is also unavailable; guarded no-payment confirmation starts only after trusted acceptance. A pre-existing `ReservationHoldChangedV1` consumer dispatches positive-price provisioning or zero-price acceptance, while the five-second database due-work worker remains as recovery. Neither path keeps the creation receipt open. The frontend may poll own reads for UI progress, but that is not backend per-second Payment polling. Show `CONFIRMED` only from the committed booking state; Payment success, return URL and broker push are not confirmation.

### Cross-owner financial contracts (proposed wire signatures)

- **Preview/review:** retain server-calculated amount and a versioned immutable reviewed commercial snapshot, bound to Account/vehicle/site/slot/start/end/method, total/deposit/currency, tariff version, prepayment mode, accepted coverage/version and quote/review expiry. Payment owns tariff/prepayment policies and rounding. `FULL` is documented default; no invented percent/fixed rates. Missing/invalid pricing/coverage fails closed, not zero. A distinct Payment quote entity is optional but accepted-version semantics are required.
- **Order preparation/combined acceptance:** `PUT /internal/reservation-deposit-orders/{operationId}` is a *proposed* protected S2S endpoint after hold commit. It must atomically validate/accept reviewed price/coverage and create or return exactly one `RESERVATION_FEE` order; no separate `AcceptQuote` HTTP endpoint. Request binds actor/scope IDs, reservation/workflow/order operation, accepted terms identity/version and immutable amounts, payment method and **exact `H` as `expiresAt`**. Same key/payload → same `CREATED(orderId)`; mismatch → `409`; unknown network result → `UNKNOWN` and same-ID recovery; definitive absence after H → durably fenced `CLOSED_NO_ORDER`. No new order or checkout at/after H.
- **Checkout:** authenticated Driver uses the existing Payment `POST /api/v1/payments/orders/{orderId}/checkout`. Verify order Account ownership and real provider-backed QR/URL; a booking ID is never an order ID. A wrong-order/foreign-account request must fail without leaking existence or charging.
- **Verified collection message:** Operations source currently defines `SmartParking.OperationService.Application.Reservations.ReservationDepositCollectedV1` with `EventId`, `CollectionId`, `PaymentOrderId`, `PaymentTransactionId`, `OperationId`, `ReservationId`, `AccountId`, `TenantId`, `SiteId`, `QuoteId`/version, Operations `TermsId`/version, amount/currency and provider reference/verification/occurrence timestamps. It configures exchange `smartpark.payment.reservation-deposit-collected.v1` and receive queue `smartpark-operation-reservation-deposit-collected-v1`; its MassTransit message URN follows that CLR namespace/type. Operations registers a consumer and validates the persisted reservation/order/account/site/amount/currency/quote/terms binding before guarded MediatR confirmation. Business idempotency is based on the locked reservation's `CollectionId` and confirmation state; this endpoint does **not** currently configure MassTransit EF inbox middleware or persist a general event-ID inbox. Payment's producer identity, schema compatibility, publisher ACL/topology and delivery guarantees still require owner agreement and live verification. The existing generic `PaymentSucceededV1` is not reservation collection evidence; its Operations consumer remains for gate-settlement handling only.
- **Targeted recovery:** restricted `GET` lookup for all distinct collections and a keyed reconciliation/refund-acceptance operation for `UNKNOWN`, missed events, late/additional money and terminal receipts; no default per-second Payment collection sweep. An accepted refund request is not payout.

### Confirmation execution contract

The still-live hold may **eventually** use only a locked Operations aggregate/domain transition and persisted claim evidence, **after** a verified Facility+Operations+Gate writer-serialization/invalidation gate. Until proof, retain current confirmation Facility acquire/complete dependency and fresh capacity/plate/site evaluation. Never use `PENDING_PAYMENT` status alone as evidence of a valid hold: evaluate authoritative H and T. After expiry/invalidation, acquire and revalidate compatible claim before T, or reconcile. One Operations DB `evaluatedAt` strictly before T determines eligibility; a pre-T evaluation may commit after T, but cannot be exposed before commit. Rollback re-evaluates with a new timestamp. Zero price requires genuine owner-issued free terms without synthetic collection.

**Implementation status:** API shapes above remain proposals, not implemented Payment route declarations. The event DTO/exchange/queue above are present as an Operations-side source contract, but are not yet a jointly approved or live-verified Payment contract. Confirm exact cross-service DTO compatibility, message identity, authorization/publisher ACL, durable outcome semantics and any inbox/reconciliation requirements before enabling end-to-end sales. Preserve existing owner port isolation, MediatR, Domain aggregate, repository/UoW and transactional outbox conventions.

### Operations integration baseline — 12 November 2026

- Operations owns `IReservationCommercialOwner` (`PreviewAsync`, `QuoteAsync`, `EnsureOrderAsync`, `AcceptZeroPriceAsync`, `CollectionsAsync`, `ReconcileAsync`), reservation commands/handlers and the `ReservationRow` lifecycle. `EnsureOrderAsync` returns `CREATED` with the authoritative `OrderId`, `UNKNOWN`, or `CLOSED_NO_ORDER`; the order result must echo the exact accepted request. Operations persists that order ID without substituting the reservation ID.
- Operations owns `ReservationHoldChangedV1` and its `HOLD_CREATED` receipt boundary. The transactional Bus Outbox is registered against the same `ReservationDbContext`; the hold consumer is registered with admission and dispatches positive-price order provisioning or genuine zero-price acceptance. `ReservationDepositCollectedV1` dispatches `RecordReservationDepositCollectedCommand`, which validates the immutable binding and starts guarded paid confirmation. `ConfirmPaidReservationCommand` / `ConfirmZeroPriceReservationCommand` preserve the Facility dependency, plate/capacity guards and authoritative pre-T evaluation.
- Real adapters: `ReservationFactsPublisher` is the MassTransit publisher when reservation messaging is enabled; `ReservationFacilityClient` supplies read/acquire/complete/abort with the existing service identity; `ReservationVehicleClient` resolves the User-owned binding. Stub: `UnavailableReservationCommercialOwner` is the registered default for all Payment-owned commercial operations, including preview, quote, order, zero-price acceptance, collection lookup and reconciliation. No Payment HTTP endpoint is declared by this API document.
- **Integration-ready at source boundary, not end-to-end ready:** the Operations ports, commands, event DTO and consumer registrations exist. Payment's producer/accepted-terms implementation, real `EnsureOrder`, publisher topology/identity agreement, outcome/reconciliation delivery and live verification are pending. Do not treat these proposals or the local DTO as proof that cross-service delivery works.
