# SPARK-273 — Cross-team implementation alignment and handoff

**Prepared:** 11 November 2026

**Scope:** Booking through `CONFIRMED`, CAR / Specific Slot. Post-confirmation lifecycle remains a separately tracked phase.

**Status update — 12 November 2026:** Local Operations source contains the hold-triggered order/zero-price dispatch, reservation-deposit consumer, guarded paid/zero-price confirmation and their DI/MassTransit registrations. The Payment commercial adapter is still an unavailable stub; the producer contract, cross-owner message/topology agreement and live Payment/Facility checks remain pending. Source review is not end-to-end verification.

**Primary decision:** [11 November architecture decision](../decision-log/2026-11-11-reservation-event-driven-confirmation.md). The [10 November evaluation-time checkpoint](../decision-log/2026-11-10-reservation-evaluation-checkpoint.md) remains in force.

## Team-level outcome

The **creation workflow ends at the committed `PENDING_PAYMENT` hold**. PaymentService creates/replays a real deposit order for that committed reservation and publishes a durable **verified collection event** after provider verification. A separate Operations consumer confirms through the guarded domain transition, not from the browser and not from continuous Payment polling. The **aggregate-only fast path is conditional on verified Facility/writer invariants**; the current Facility confirmation dependency remains necessary until that gate passes. After H, pre-T verified money may attempt fresh compatible reacquisition; otherwise reconcile/refund without a booking. At/after T, no ordinary new confirmation.

## Sequence and owner responsibilities

| Step | Operation | Owner | Durable result / handoff |
|---|---|---|---|
| 1 | Choose CAR, specific slot, interval, payment method; preview price | Driver UI; Payment authoritative price | Preview is informational; no claim/order |
| 2 | Review binding and versioned tariff/deposit/coverage and Operations policies | Operations orchestrates Payment quote + Facility/policy read | `termsId`, `termsVersion`, expiry, immutable reviewed snapshot; no claim/order |
| 3 | Submit one idempotent reservation intent | Driver → Operations | Durable request + Account-current authorization work; 202 is not a hold |
| 4 | Check vehicle, Facility admission, plate/site/quota/claim and terms under guards | Operations + Facility/User authoritative facts | `PENDING_PAYMENT` reservation, exclusive claim, exact H, durable hold/outbox; creation receipt terminates successfully |
| 5 | Ensure the exact deposit order *after* hold commit | Operations order worker → Payment S2S | One immutable Payment order or authoritative `UNKNOWN` / `CLOSED_NO_ORDER`; no owner HTTP in guarded transaction |
| 6 | Pay via an actual order using existing checkout API | Driver UI → Payment → payOS | Provider QR/URL returned by payOS; no local placeholder; no money inferred from redirect |
| 7 | Verify provider collection and emit outbox event | Payment | One `collectionId` per distinct verified payment, immutable order/terms binding; durable publish/replay |
| 8 | Consume and confirm | Operations | Deduplicated money evidence, guarded checkpoint before T, `CONFIRMED` + effect/outbox atomically; or recovery/reconciliation |
| 9 | Resume/read | Driver UI | `GET /api/me/reservations/{id}` is entitlement authority; account-scoped SignalR may trigger refresh only |

**Zero price:** Real owner-issued free/covered terms bypass order/checkout and use the separate Operations no-payment confirmation route with the same checkpoint and capacity safety; no simulated payment.

## Operations source integration handoff — 12 November 2026

This section records the current local Operations contracts, not new Payment wire contracts. **Approved behavior** is the creation-at-hold boundary, immutable accepted terms/order binding, guarded confirmation, existing Facility dependency and the strict `evaluatedAt < T` cutoff. Exact Payment HTTP routes, machine-auth claims, producer DTO schema ownership, exchange/queue ACLs and durable decline delivery remain **unapproved integration details** until both owners review them.

### Operations-owned interfaces and commands

| Operations port/command | Current responsibility | Integration state |
|---|---|---|
| `IReservationCommercialOwner.PreviewAsync` / `QuoteAsync` | Obtain Payment-owned price preview and versioned terms before a hold | Port exists; registered `UnavailableReservationCommercialOwner` blocks live use |
| `IReservationCommercialOwner.EnsureOrderAsync(DepositOrderRequest)` | Create/replay the exact order after the committed hold; stable `OperationId` is the request ID; result carries the authoritative `OrderId` and echoes the accepted request | Application/domain state and handler exist; real Payment adapter is a stub |
| `IReservationCommercialOwner.AcceptZeroPriceAsync(ZeroPriceAcceptanceRequest)` | Obtain owner acceptance for genuine zero-price/free coverage without a Payment order | Port and guarded handler exist; real owner acceptance is a stub |
| `IReservationCommercialOwner.CollectionsAsync` / `ReconcileAsync` | Targeted lookup of collection evidence and idempotent reconciliation acceptance after a failed entitlement decision | Ports exist; adapters are stubs. Lookup is not used for normal-path polling |
| `ProvisionReservationOrderCommand`, `AcceptZeroPriceTermsCommand` | Run owner calls outside Operations' guarded booking transaction, then persist outcomes under guards | Implemented; hold-changed event dispatches these commands and the existing five-second worker remains a recovery mechanism |
| `RecordReservationDepositCollectedCommand` | Validate a Payment collection against persisted order/reservation/Account/tenant/site/quote/terms/amount/currency and initiate confirmation | Implemented and consumer-registered; producer and cross-owner contract remain unverified |
| `ConfirmPaidReservationCommand`, `ConfirmZeroPriceReservationCommand` | Apply Facility confirmation dependency, plate/capacity guards and authoritative pre-T evaluation; commit `CONFIRMED` and Operations outbox effects | Implemented; Facility dependency retained; no fast-path gate has passed |
| `ExpireReservationHoldCommand` | Enforce the authoritative database H deadline and release the claim | Implemented; current discovery is periodic, not a durable scheduled command at H |

Operations also owns `ReservationHoldChangedV1`, `ReservationConfirmedV1`, booking state, capacity/plate claims and the transactional Bus Outbox in its own `ReservationDbContext`. `HOLD_CREATED` is the terminal successful creation-request outcome; the reservation remains `PENDING_PAYMENT`. `PaymentOrderId` is the authoritative nullable Payment identity; the reservation ID must never be used as an order ID.

For the Payment review, start at [BookingContracts.cs](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/BookingContracts.cs) (`IReservationCommercialOwner`, `DepositOrderRequest`, `ReservationDepositCollectedV1`), [ReservationOrderProvisioning.cs](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationOrderProvisioning.cs) (`EnsureOrder` and zero-price calls), [ReservationConfirmationHandlers.cs](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationConfirmationHandlers.cs) (collection binding and guarded confirmation), [ReservationOwners.cs](../../../src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/ReservationOwners.cs) (real/stub adapters), [ReservationMessaging.cs](../../../src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/ReservationMessaging.cs) (endpoints, routing and outbox gates), [ReservationBooking.cs](../../../src/Services/OperationService/SmartParking.OperationService.Domain/Entities/ReservationBooking.cs) (state invariants), and [ReservationBookingRepository.cs](../../../src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/ReservationBookingRepository.cs) (authoritative DB-time and due-work queries). Leave [ReservationOrderProvisioningWorker.cs](../../../src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/ReservationOrderProvisioningWorker.cs) unchanged until its separately deferred removal is approved.

### Current event identities and binding

| Current Operations source DTO | Current configured exchange / receive queue | Meaning and limit |
|---|---|---|
| `ReservationHoldChangedV1` (`SmartParking.OperationService.Application.Reservations`) | `smartpark.operations.reservation-hold-changed.v1` / `smartpark-operation-reservation-hold-changed-v1` | Operations-owned hold/expiry fact. For `PENDING_PAYMENT`, consumer reloads current booking state and dispatches positive-deposit provisioning or zero-price acceptance. |
| `ReservationDepositCollectedV1` (`SmartParking.OperationService.Application.Reservations`) | `smartpark.payment.reservation-deposit-collected.v1` / `smartpark-operation-reservation-deposit-collected-v1` | Operations' current inbound V1 DTO. Contains `EventId`, `CollectionId`, `PaymentOrderId`, `PaymentTransactionId`, `OperationId`, reservation/account/tenant/site IDs, quote and Operations terms IDs/versions, amount/currency, provider/reference and verification/occurrence timestamps. Its MassTransit URN follows the CLR namespace/type; Payment must independently match the actual DTO envelope and identity before publishing. |
| `ReservationConfirmedV1` (`SmartParking.OperationService.Application.Reservations`) | `smartpark.operations.reservation-confirmed.v1` | Operations-owned committed entitlement fact; event ID is the stable confirmation effect ID. It is not a Payment refund/decline result. |

The collection consumer checks the immutable persisted reservation/order/account/tenant/site/quote/terms/amount/currency binding under the Operations guards. Duplicate handling currently relies on the locked booking's `CollectionId` and confirmation state. **There is no EF Inbox middleware on this Payment endpoint and no general persisted `EventId` inbox.** A second distinct collection is not made into a new entitlement; its Payment-owned financial disposition and reliable Operations outcome delivery remain an integration gap. Do not equate MassTransit retry with end-to-end exactly-once.

The order request binds the stable `OperationId`/request ID, `ReservationId`, authenticated Account and tenant/site, selected vehicle/slot/interval/method, Operations terms ID/version, the accepted quote snapshot (quote ID/version, Account/selection, amount/deposit/currency, coverage version/disclosure) and exact `ExpiresAt=H`. Payment must return the actual authoritative `OrderId`; Operations persists it immutably. The inbound collection is checked against that saved binding before confirmation. Payment must preserve the same event/collection identity and payload on redelivery; a changed order request under the same operation identity is not a replay.

### Expected Payment-owned operations and events

- Implement server-authoritative preview and versioned accepted terms; preserve the exact reviewed quote, selected inputs, coverage evidence/version, amount, deposit, currency and expiry. No client-supplied amount, fabricated zero, or silent reprice.
- Implement the approved logical `EnsureOrder` operation after hold commit. The same operation ID plus same immutable payload returns the same actual order; mismatched replay conflicts; ambiguous results stay `UNKNOWN`; definitive no-order closure is durable. Bind order expiry to exact H. The proposed `PUT /internal/reservation-deposit-orders/{operationId}` path is **not approved wire contract**.
- Implement separate genuine zero-price acceptance with authoritative free/coverage proof and no synthetic order.
- Publish one durable, owner-authenticated `ReservationDepositCollectedV1` (or an owner-agreed compatible schema) for every distinct provider-verified collection, including late/additional collections. Event/collection IDs and immutable order/reservation/terms/amount/currency/provider binding must remain stable on redelivery.
- Provide restricted targeted collection lookup and durable idempotent reconciliation/refund status, plus an agreed reliable confirmation/decline outcome path. An accepted reconciliation/refund request is not payout evidence. Operations owns entitlement decisions; Payment owns collection, order and refund facts.

### Idempotency, delivery and configuration

- Operations uses request ID as stable order `OperationId`, accepts only an echoed exact `DepositOrderRequest`, persists one authoritative `PaymentOrderId`, and rejects reassignment to a different order. Same `CollectionId` is idempotent under the locked reservation; `ConfirmationEffectId` remains stable for the committed confirmation event.
- When `ReservationAdmission:Enabled=true`, `ReservationDbContext`'s MassTransit EF Bus Outbox captures Operations state changes and messages in the same owning context/transaction. `Messaging:Enabled=true` registers the Payment deposit consumer (and the existing gate `PaymentSucceededV1` consumer); `Messaging:OutboxDeliveryEnabled=true` enables relay. With relay disabled, captured events remain pending and require backlog monitoring.
- Configure `SMARTPARK_RABBITMQ_HOST`, `SMARTPARK_RABBITMQ_PORT`, `SMARTPARK_RABBITMQ_VHOST` and least-privilege broker credentials (`ReservationAdmission:BrokerUser/Password` or the supported `SMARTPARK_RABBITMQ_USER/PASSWORD`) in the service environment. Provision durable exchanges, bindings/queues, retry/error topology and publisher/consumer ACLs before enabling relay. The Payment consumer needs the Operations admission/outbox database schema and both messaging gates; configuration does not provision the broker or prove Payment compatibility.
- Facility reads/acquire/complete/abort use the existing `ReservationFacilityClient` and dedicated Facility machine-auth configuration. Keep the confirmation dependency active. The commercial adapter has no configured Payment HTTP client or agreed Payment issuer/audience/permission contract.

### Integration-ready versus blocked

**Ready at Operations source boundary:** hold/receipt persistence and outbox capture, event-triggered order/zero-price commands, idempotent order identity persistence, local collection DTO/consumer registration, immutable binding validation, guarded confirmation and Facility coordination are present. **Blocked:** real Payment preview/quote/order/zero-price/lookup/reconciliation adapter, Payment producer and provider verification, jointly agreed event identity/topology/authentication, reliable decline/outcome delivery, and all live cross-service acceptance. Do not claim end-to-end reservation/payment success until the actual Payment implementation is reviewed and verified.

## Payment teammate — must deliver

- Authoritative versioned reservation preview/review using the existing pricing engine where suitable. `PREPAYMENT_MODE`/percent/fixed are Payment-owned. No universal 30%/50% amount or missing-price-to-zero fallback. Preserve the quote/snapshot identity, tariff version, exact covered amount, deposit, selected interval and method, coverage reference/owner evidence, disclosure and quote validity. The Driver must not be charged a newer unreviewed tariff.
- Merge **commercial acceptance with idempotent `EnsureOrder`**; do not create a separate `AcceptQuote` S2S endpoint. Only create/replay an order after Operations commits a hold. Validate accepted terms and coverage, bind exact H as order expiry, return same `orderId` for the same operation/request fingerprint, `409` on mismatched replay; distinguish `CREATED`, `UNKNOWN`, `CLOSED_NO_ORDER` with durable closure fencing. On a rejected/stale quote, Operations releases the hold/requests re-review. At H no new order or checkout attempt.
- Reuse the existing `RESERVATION_FEE`, `RESERVATION`, payOS adapter, signature verification and outbox. Harden authorization: Driver can initiate checkout/read only their own order; dedicated Operations machine permission for internal order and collection endpoints; no browser amount/Account override or insecure default JWT fallback.
- Persist a provider attempt intent **before** side effects, keep order vs checkout idempotency separate, replay actual provider QR/URL, resolve `UNKNOWN` by same provider identity before retries, obey four total attempts only for definitive retryable failure. Do not silently assume network timeout means failure.
- Emit authenticated, transactional-outbox financial facts for every distinct collection including after order PAID/EXPIRED/CANCELLED, with immutable reservation/account/site/tenant/quote/amount/currency/provider binding; provide restricted collection lookup for targeted recovery, and idempotent reconciliation/refund status. A refund accepted into a queue is not a completed payout.
- Supply complete independently versioned API/message DTOs, 401/403/409/error semantics, OpenAPI samples, RabbitMQ message identity/exchange/queue + ACL, required issuer/audience/permission, configurable tariffs, startup notes and tests. Exact paths/event name are **contract proposals** until owners agree.

## Operations teammate — source baseline present; validation and outcome integration remain

- Keep current request admission, one-time Account-current decision, vehicle binding and Facility admission; make committed hold the successful **request workflow** terminal point. Keep reservation `PENDING_PAYMENT` until separately confirmed. Preserve response compatibility by explicitly versioning/documenting any new request receipt outcome (suggested `HOLD_CREATED`).
- Persist a stable order-intent identity with hold/outbox; outside the Operations DB transaction, invoke idempotent Payment order provisioning; associate the returned order in a separate guarded UoW. Resolve `UNKNOWN` without second orders. Do not allow a stale quote rejection to strand an unpaid active hold; durable release/cleanup and client renewed-review result are required.
- The hold consumer, MassTransit Payment-collection consumer, MediatR confirmation commands, immutable collection validation and current database-backed recovery worker are present in local source. Do not add a new worker or inbox here; confirm the consumer idempotency and reliable decline/outcome requirements with Payment before changing this slice.
- **Before fast-path proof** keep Facility confirmation dependency and guarded capacity/plate/site validation. **After proof** a live hold may confirm from locked Operations aggregate and persisted claim invariants without redundant Facility/calculation calls, but must still check H, verified payment binding, current status, exactly one DB evaluation time before T, concurrency guards and durable commit.
- If H expired/claim invalidated, perform fresh guarded capacity/Facility reacquisition before T; if unavailable or `evaluatedAt >= T`, reject new entitlement and durably reconcile the collected money. Never reopen a committed confirmed booking just because H passes.
- No service call inside held site/plate DB locks; no general-purpose Saga framework, local payment poll loop or fake pricing/payment fallback.

## Facility teammate — must deliver / prove

- Retain existing dedicated Operations-issued RS256 machine auth, acquire/complete/fenced-abort contract and source-owned writer coordination. A Facility admission dependency ends at hold commit; it does not automatically protect all subsequent Facility mutations until payment.
- Enumerate/edit each mutation of active site/slot/unit, physical Gate usage, deletion/closure, scheduled maintenance activation and any future tenant writer. Demonstrate it cannot silently overwrite a live Operations claim. Prove synchronous fencing or a binding/projection that invalidates the aggregate safely before confirmation, including crash/replay.
- Until proved, **do not remove confirmation `acquire`**. Tests must cover conflicting edit, concurrent entry, delayed notifications, reconnect, two-service partial failure, and direct-write bypass risks. Expired hold requires a fresh dependency and capacity evaluation.

## Frontend teammate — must deliver

- Preserve present slot/time/vehicle/method layout; replace local `driverData`/hourly estimate with backend authoritative review while keeping preview feedback. Submit `POST /api/reservation-requests` with one stable UUID key and accepted `termsId/termsVersion`.
- Treat `202`/request processing separately from hold success, and hold success separately from `CONFIRMED`. Use receipt/own booking to learn reservation ID, H and **real** `paymentOrderId`; do not substitute booking ID as order ID. Pass actual order to authenticated Payment checkout; do not render fake bank/QR values.
- Display countdown from server H; disable new checkout at/after H; Payment/Operations may still reconcile late settlement. After payment or refresh, resume same request and own booking. Display confirmation only from committed Operations `CONFIRMED`; display financial reconciliation/refund status independently. A Payment success event or SignalR push alone is notification.
- Stale quote, producer outage and ambiguous checkout do not create an automatic new intent/order/charge; prompt explicit fresh review when appropriate.

## QA and release evidence

- Check full hold (including overlapping competing booking), no-capacity and invalid vehicle cases, unchanged tariff replay, changed tariff before order, coverage invalidation, stale/expired quote, exact order H and order creation at H, lost HTTP acknowledgement, multiple identical keys and payload conflict.
- Exercise Payment provider success and duplicate/delayed/extra collection, cancelled/expired order settlement, UNKNOWN retry fencing, provider QR authenticity and authorization tampering.
- Exercise event redelivery/reordering, consumer crash, lost confirmation commit acknowledgment, wrong amount/terms/account, collection after terminal receipt, refund acceptance vs payout.
- Exercise **held-fast-path proof**: every Facility/Operations/Gate writer, asynchronous structure delivery gap, direct SQL/bulk-write bypass analysis, same-plate cross-site entry, quota/claim exclusivity. Keep baseline confirmation acquire enabled until the proof is signed off.
- Exercise expired-H pre-T capacity reacquisition success/failure; `evaluatedAt` before T with commit after T (qualifies), evaluation exactly/after T (fails), rollback/retry after T (fails). Genuinely free booking still needs guards.
- Solution build: passed on 12 November with zero errors and existing NuGet advisory warnings (NU1902 MessagePack 2.5.302, NU1903 SignalR StackExchangeRedis 10.0.12); see the [SPARK-273 session](../../../ai/tasks/active/TASK-SPARK-273-SESSION-20261108.md). Unit/integration/manual/runtime validation remains **NOT RUN — USER VERIFICATION REQUIRED**. Do not infer live readiness from a source contract or Jira status.

## Deferred Implementation Backlog — Post Reservation/Payment Integration

These are deferred repository backlog items, not implemented work or Jira tickets. Revisit only after the reservation/Payment integration gate and required owner approvals.

### BACKLOG A — Durable hold-expiry scheduling

- **Current limitation:** H is an authoritative database deadline. `DueHoldExpiryAsync` discovers due holds through the existing five-second `ReservationOrderProvisioningWorker`; no approved durable scheduled command is delivered at H.
- **Failure scenario and impact:** Operations is stopped or the scan is delayed across H. The row can remain visibly `PENDING_PAYMENT` and its claim/outbox release is delayed; a delayed UI state and capacity churn result. Eligibility still must not extend past H.
- **Existing protection:** Handlers evaluate authoritative DB time and H under booking/plate/site guards; admission/confirmation cannot treat a stale pending status as a live hold; due queries recover work after restart.
- **Proposed minimum solution:** After approval, enqueue one logical expiry command at H using an approved durable scheduler. The command re-reads DB time and current state, is idempotent, and writes the release plus availability/outbox fact atomically; early/duplicate/stale commands safely re-evaluate.
- **Service owner:** Operations owns expiry and reservation state; platform/infrastructure owner supplies any approved scheduler.
- **Dependencies and approval:** Select and approve the scheduler mechanism and infrastructure, delivery/retention/failure behavior, topology and deployment changes. No scheduler/package choice is approved by this task.
- **Acceptance criteria:** One logical expiry action per hold; restart/outage and duplicate delivery do not lose or duplicate release; H remains authoritative; claim/plate/capacity guards and atomic outbox behavior remain intact.
- **Verification required later:** PostgreSQL + scheduler integration tests for exact H/equality, early and duplicate delivery, outage/restart, commit loss and concurrent confirmation; verify no premature expiry or capacity release.

### BACKLOG B — Event-driven confirmation continuation and recovery

- **Current limitation:** Paid/zero-price confirmation state is durable and the current database-backed worker can resume due work, but Facility readiness does not independently trigger a complete durable confirmation continuation. Confirmation checks are still discovered by the five-second scanner.
- **Failure scenario and impact:** Facility becomes ready just after a scan, or Operations is unavailable while it becomes ready; confirmation waits for another scan or host recovery, delaying entitlement and extending reservation-state/capacity inconsistency.
- **Existing protection:** Persisted confirmation state, retry deadline, Facility dependency state, current worker scanning, and guarded idempotent confirmation survive process restart; H/T and capacity/plate checks are repeated before transition.
- **Proposed minimum solution:** Review the existing Facility dependency ready/complete transition and add the smallest owner-approved durable continuation trigger at the prerequisite-ready commit. Keep a bounded, restart-safe recovery path without normal-path reservation polling.
- **Service owner:** Operations owns confirmation; Facility owns dependency readiness. Both owners approve the continuation event/command boundary.
- **Dependencies and approval:** Review Facility dependency transitions, transaction/outbox capability and failure semantics; agree one durable mechanism and retry bounds. Do not add a new state machine or worker before approval.
- **Acceptance criteria:** Readiness schedules continuation durably once; duplicate/out-of-order readiness is harmless; crash before/after dispatch resumes; no normal-path due-table polling is needed; Facility dependency, capacity, plate and pre-T guards are unchanged.
- **Verification required later:** Cross-service broker/database tests for readiness before/after scan timing, duplicate delivery, service outage/restart, partial commit, bounded retry, and concurrent invalidation/confirmation.

### BACKLOG C — Deprecated worker removal

- **Current limitation:** `ReservationOrderProvisioningWorker` still performs five-second scans for order provisioning, hold expiry, zero-price acceptance and paid/zero-price confirmation. Its repository `Due*Async` queries and DI registration remain active.
- **Failure scenario and impact:** Removing it before every responsibility has a safe replacement can strand orders, leave holds unreleased, or prevent eligible confirmation/recovery after restart.
- **Existing protection:** The worker and due queries remain registered; each handler rechecks durable state and authoritative DB time under existing guards. The hold event is an additional order/zero-price trigger, not a complete replacement for recovery.
- **Proposed minimum solution:** Remove the worker, only-obsolete due queries and DI registration in a dedicated cleanup slice after BACKLOG A and B and order-provisioning recovery have proven replacements for every current responsibility.
- **Service owner:** Operations.
- **Dependencies and approval:** BACKLOG A and BACKLOG B must be resolved; Payment integration must establish safe idempotent order recovery. Preserve `ReservationOrderProvisioningWorker.cs` unchanged until then.
- **Acceptance criteria:** No due-work responsibility is lost; restart/outage recovery remains durable; all replacement paths are registered and tested; obsolete queries are removed only when no callers remain.
- **Verification required later:** Source/DI call-site audit plus PostgreSQL/broker restart, outage, duplicate and concurrent-claim tests across every replaced work type.

### BACKLOG D — PaymentService confirmation-outcome integration

- **Current limitation:** Operations contains the confirmation and some decline/reconciliation business logic, but the registered real Payment adapter is absent. `CollectionsAsync` and `ReconcileAsync` are stubbed; no jointly approved durable confirmation/decline delivery contract exists. A mismatched binding does not mutate the hold; current consumer does not create an outcome message. Distinct additional collections do not grant another entitlement and have no completed Operations-to-Payment disposition path here.
- **Failure scenario and impact:** A provider-verified collection arrives for a stale/invalid booking, or an extra distinct collection is delivered. Operations correctly withholds entitlement, but Payment may not durably learn the confirmation/decline/reconciliation outcome; money may remain unresolved or the UI may misstate financial status.
- **Existing protection:** Operations validates the persisted order/reservation/account/tenant/site/quote/terms/amount/currency binding, only confirms under Facility/capacity/plate/pre-T guards, and uses `CollectionId`/confirmation state to avoid duplicate entitlement. The existing reconciliation call is fail-closed behind the unavailable adapter; it is not proof of refund.
- **Proposed minimum solution:** After Payment implementation and owner review, wire the agreed Payment ports and durable producer/consumer outcome contracts. Send/replay stable collection and confirmation/decline identities; Payment remains authoritative for order, collection and refund status, while Operations remains authoritative for entitlement.
- **Service owner:** Joint: Operations owns confirmation/decline; Payment owns financial collection and reconciliation/refund execution.
- **Dependencies and approval:** User review of PR #98 / SPARK-391 and the Payment team's actual implementation; cross-owner agreement on DTO/message identity, outcome semantics, auth/topology and idempotency before wiring. Do not invent a new endpoint or message now.
- **Acceptance criteria:** Verified exact binding; durable, idempotent and observable confirmation/decline/reconciliation outcomes; duplicate, late and additional collections cannot duplicate entitlement or disappear financially; refund acceptance is distinct from payout; Operations never owns financial settlement.
- **Verification required later:** Real Payment+Operations contract and broker/outbox tests for valid/mismatched bindings, duplicate/redelivered/late/additional collections, lost acknowledgements, confirmation vs decline, reconciliation acceptance/status and eventual payout evidence.

## Contract boundaries and follow-up

**Approved:** creation boundary, event-driven money→Operations confirmation, conditional aggregate fast path, expired/invalidated hold reacquisition, versioned pricing, combined quote acceptance with EnsureOrder, existing H/T and payout policies. **Owner agreement still needed:** schema/endpoint/event names, Service-to-Service token format, quote persistence/coverage race protocol and concrete Facility writer proofs. Cross-owner agreements must be independently versioned and not introduce a common `src/Shared` build dependency.

Related documents: [Payment owner](2026-11-09-reservation-producer-handoff.md), [Facility dependency](2026-11-09-reservation-facility-durable-dependency.md), [Events/availability](2026-11-10-reservation-events-and-facility-integration.md), [Frontend](2026-11-09-reservation-frontend-handoff.md), [Completion checklist](2026-11-10-reservation-completion-checklist.md), [API design](../../03-design/api/reservation-first-slice-api-design.md) and [QA](../../04-testing/reports/SPARK-273-deployment-checklist.md).
