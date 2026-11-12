# SPARK-273 — Cross-team implementation alignment and handoff

**Prepared:** 11 November 2026

**Scope:** Booking through `CONFIRMED`, CAR / Specific Slot. Post-confirmation lifecycle remains a separately tracked phase.

**Status:** Approved workflow direction; implementation/producer/Facility fast-path verification remains pending. No code, Jira, deploy or runtime-test changes are claimed.

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

## Payment teammate — must deliver

- Authoritative versioned reservation preview/review using the existing pricing engine where suitable. `PREPAYMENT_MODE`/percent/fixed are Payment-owned. No universal 30%/50% amount or missing-price-to-zero fallback. Preserve the quote/snapshot identity, tariff version, exact covered amount, deposit, selected interval and method, coverage reference/owner evidence, disclosure and quote validity. The Driver must not be charged a newer unreviewed tariff.
- Merge **commercial acceptance with idempotent `EnsureOrder`**; do not create a separate `AcceptQuote` S2S endpoint. Only create/replay an order after Operations commits a hold. Validate accepted terms and coverage, bind exact H as order expiry, return same `orderId` for the same operation/request fingerprint, `409` on mismatched replay; distinguish `CREATED`, `UNKNOWN`, `CLOSED_NO_ORDER` with durable closure fencing. On a rejected/stale quote, Operations releases the hold/requests re-review. At H no new order or checkout attempt.
- Reuse the existing `RESERVATION_FEE`, `RESERVATION`, payOS adapter, signature verification and outbox. Harden authorization: Driver can initiate checkout/read only their own order; dedicated Operations machine permission for internal order and collection endpoints; no browser amount/Account override or insecure default JWT fallback.
- Persist a provider attempt intent **before** side effects, keep order vs checkout idempotency separate, replay actual provider QR/URL, resolve `UNKNOWN` by same provider identity before retries, obey four total attempts only for definitive retryable failure. Do not silently assume network timeout means failure.
- Emit authenticated, transactional-outbox financial facts for every distinct collection including after order PAID/EXPIRED/CANCELLED, with immutable reservation/account/site/tenant/quote/amount/currency/provider binding; provide restricted collection lookup for targeted recovery, and idempotent reconciliation/refund status. A refund accepted into a queue is not a completed payout.
- Supply complete independently versioned API/message DTOs, 401/403/409/error semantics, OpenAPI samples, RabbitMQ message identity/exchange/queue + ACL, required issuer/audience/permission, configurable tariffs, startup notes and tests. Exact paths/event name are **contract proposals** until owners agree.

## Operations teammate — must deliver

- Keep current request admission, one-time Account-current decision, vehicle binding and Facility admission; make committed hold the successful **request workflow** terminal point. Keep reservation `PENDING_PAYMENT` until separately confirmed. Preserve response compatibility by explicitly versioning/documenting any new request receipt outcome (suggested `HOLD_CREATED`).
- Persist a stable order-intent identity with hold/outbox; outside the Operations DB transaction, invoke idempotent Payment order provisioning; associate the returned order in a separate guarded UoW. Resolve `UNKNOWN` without second orders. Do not allow a stale quote rejection to strand an unpaid active hold; durable release/cleanup and client renewed-review result are required.
- Add a dedicated MassTransit Payment-collection consumer and MediatR confirmation command, deduplication/inbox, immutable collection validation, separate expiry/reconciliation worker, after-terminal collection handling, and crash/replay recovery.
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
- Build and runtime validation are **not yet done** by this documentation package. Do not infer live readiness from a new contract or marked Jira ticket.

## Contract boundaries and follow-up

**Approved:** creation boundary, event-driven money→Operations confirmation, conditional aggregate fast path, expired/invalidated hold reacquisition, versioned pricing, combined quote acceptance with EnsureOrder, existing H/T and payout policies. **Owner agreement still needed:** schema/endpoint/event names, Service-to-Service token format, quote persistence/coverage race protocol and concrete Facility writer proofs. Cross-owner agreements must be independently versioned and not introduce a common `src/Shared` build dependency.

Related documents: [Payment owner](2026-11-09-reservation-producer-handoff.md), [Facility dependency](2026-11-09-reservation-facility-durable-dependency.md), [Events/availability](2026-11-10-reservation-events-and-facility-integration.md), [Frontend](2026-11-09-reservation-frontend-handoff.md), [Completion checklist](2026-11-10-reservation-completion-checklist.md), [API design](../../03-design/api/reservation-first-slice-api-design.md) and [QA](../../04-testing/reports/SPARK-273-deployment-checklist.md).
