# SPARK-273 — Short creation workflow, event-driven payment confirmation and conditional aggregate fast path

**Decision date:** 11 November 2026 (Asia/Ho_Chi_Minh)

**Decision status:** **APPROVED** for the architecture choices explicitly recorded below; transport DTOs/route names shown as proposals remain **TO AGREE**.

**Scope:** CAR / Specific Slot reservation creation through durable confirmation. Allocation, gate/scan, cancellation UI and checkout remain separate phase work except guards/recovery needed for safe confirmation.

**Source:** User-approved SPARK-273 decisions, the Payment teammate's 11 November alignment memo and source inspection of `feature/SPARK-273-driver-book-parking-ahead-of-arrival` at `8adf317304988098c51a948faad28a940b21667b`. **Documentation decision, not evidence of implemented, built, tested or deployed behavior.**

This decision amends the workflow design in the [reservation API specification](../../03-design/api/reservation-first-slice-api-design.md) and the existing [Payment](../handoffs/2026-11-09-reservation-producer-handoff.md), [Facility](../handoffs/2026-11-09-reservation-facility-durable-dependency.md), [frontend](../handoffs/2026-11-09-reservation-frontend-handoff.md) and [QA](../../04-testing/reports/SPARK-273-deployment-checklist.md) handoffs. The [10 November confirmation evaluation checkpoint](2026-11-10-reservation-evaluation-checkpoint.md) remains authoritative for *when* confirmation may occur. The [business rules](../../01-requirements/business-rules/04-reservations-allocation.md) and [Payment rules](../../01-requirements/business-rules/06-pricing-payments.md) retain their product-level constraints.

## 1. Approved architecture decisions

### D11-A — Creation workflow finishes at the committed hold

The **reservation request/workflow** completes successfully when Operations durably commits a new `ReservationRow` in `PENDING_PAYMENT`, its authoritative temporary slot/plate/site capacity claim, the admission dependency outcome, and the corresponding durable outbox fact. `HOLD_CREATED` is a **proposed successful request receipt outcome** to avoid confusing workflow completion with the reservation's later lifecycle completion; its exact wire token must be versioned and documented before implementation.

The **reservation business lifecycle** remains `PENDING_PAYMENT → CONFIRMED → ALLOCATED → PARKING → COMPLETED` (or its explicit expiry/cancellation/failure branches). Do **not** transition a reservation from `PENDING_PAYMENT` directly to lifecycle `COMPLETED`. Do **not** reinterpret existing request `COMPLETED` responses as confirmation without an API compatibility plan.

The creation worker no longer remains in `BOOKING` for the five-minute payment period. The reservation row, hold expiry H, durable order-provisioning intent and consumer/recovery commands represent the waiting period. User authorization and vehicle binding remain part of the existing creation workflow. External financial calls must not occur under the guarded Operations database transaction.

### D11-B — Payment confirmation is a separate event-driven workflow

1. On hold commit, Operations durably schedules **one logical idempotent deposit-order provisioning operation**, processed outside the guarded transaction.
2. PaymentService creates or replays the exact order for the committed reservation; Operations persists the returned authoritative `orderId`. The frontend may only initiate checkout for that actual order.
3. PaymentService validates payOS/provider evidence, commits **each distinct verified financial collection** with a transactional outbox record, then emits a versioned, authenticated reservation collection event.
4. A separate Operations consumer deduplicates the collection/event, validates the immutable reservation/order/amount/terms binding, and requests confirmation through an Application/MediatR command.
5. Operations alone decides and commits `CONFIRMED`. The confirmation state, effect ID, approved evaluation timestamp, required dependency outcome and durable confirmation outbox fact commit atomically; only then can the client see a confirmed entitlement.
6. Driver redirects, a checkout-status success, a QR scan, or an external event alone do **not** establish a confirmed booking. Frontend notifications only prompt authoritative own-booking/receipt reads.
7. Use **targeted recovery** for outbox delivery gaps, lost order/commit acknowledgements, `UNKNOWN` provider attempts, expired holds and late/additional collections. No per-second normal-path payment polling or continuously progressing reservation Saga.

A genuine owner-authorized **zero-deposit / zero-price booking** uses a separate no-payment confirmation command with verified immutable free/coverage terms. It must not create a fake Payment order, pretend a demo payment was collected, or bypass the approved capacity and checkpoint guards.

### D11-C — Confirm through the aggregate fast path only after invariant proof

**Approved intended fast path:** For a **still-live hold** with proven protected claim invariants, lock the owning Operations aggregate and required concurrency guards, validate verified collection binding plus current state/time, and invoke an aggregate-owned `PENDING_PAYMENT → CONFIRMED` transition. There need not be a redundant external Facility read or full capacity recalculation **if** the following evidence is established:

- Every supported Operations writer affecting the site/plate/slot/claim/occupancy, including hold expiry, competing booking, cancellation and Gate physical entry, honors the same serialization/claim contract and cannot silently invalidate a live held claim.
- All Facility structural/availability/activation changes affecting that live claim are fenced by an effective integration protocol **or** durably invalidate/refuse the claim under a coordination rule that makes the Operations aggregate authoritative at commit time. An asynchronous Facility notification that may lag behind an edit is **not** sufficient evidence.
- Claim identity, booking interval plus buffer, canonical plate, applicable quota and eligibility dependencies remain current; the locked transition cannot proceed on stale or unversioned facts. No direct SQL, bulk writer, unmanaged `DbContext` write, future writer or out-of-band Facility mutation bypasses the invariant without detection.
- Confirming within a live hold never creates a duplicate entitlement, double-claims capacity, or bypasses the exact T cutoff. Record current proof, competing-writer enumeration and concurrency/lock tests in the deployment checklist.

**Implementation gate:** These properties are **not yet demonstrated** by the inspected branch. The current Facility design releases its admission dependency after hold commit and separately acquires a Facility confirmation dependency. **Retain that existing confirmation Facility coordination and capacity evaluation until the implementation + QA gate proves the aggregate-only path safe.** This approval does not authorize an immediate source-code bypass.

### D11-D — Expired/invalidated hold recovery

For a hold expired at H, a claim explicitly invalidated, or an aggregate that cannot prove its protection, no right to the original capacity is assumed. When verified money is associated with such a reservation, Operations may reacquire and revalidate a compatible claim **strictly before T** under the approved locks, the current Facility dependency and policy/plate/site facts. A competing claim wins according to the established serialization rules. If reacquisition or the pre-T checkpoint fails, do not confirm; durably accept financial reconciliation/refund disposition for that collection. Reconciliation acceptance does not mean payout is finished.

Expiry is decided using authoritative database time even if the expiry worker has not run: `Status=PENDING_PAYMENT` alone does not prove `now < H`. At H, Payment disallows *new checkout attempts/orders*; it must still record and notify Operations of money arriving afterward, including when the order already shows PAID/EXPIRED/CANCELLED. After T, an unconfirmed reservation cannot be newly confirmed even if a trusted provider says the debit occurred earlier.

### D11-E — Keep the approved 10 November cutoff

For start T, confirmation eligibility uses **one Operations database timestamp** at the final guarded backend checkpoint, strictly `evaluatedAt < T`. Verified money (or genuine zero-price eligibility) must already be available. The guarded transaction may durably commit **after** T when its accepted evaluation was before T; it is invisible until commit. A failed/rolled-back attempt gets a fresh timestamp; it cannot reuse an earlier success time. A provider payment timestamp does not backdate the Operations checkpoint. Avoid a second cutoff check at commit. This decision changes *which facts may be re-read on a proven live-hold fast path*, not the pre-T timestamp rule or durable-commit requirement.

### D11-F — Versioned pricing remains; separate acceptance is merged

PaymentService owns authoritative tariff and prepayment terms (`FULL` default; `PERCENTAGE` and `FIXED` when explicitly configured), rounding, accepted commercial version and provider collection. Operations owns the effective H (`min(hold creation + effective policy duration, T)`; ordinary default five minutes), booking capacity and confirmation. The Driver reviews a trusted, **versioned financial price/deposit offer** bound to Account/vehicle/site/slot/interval/method, with expiry, amounts/currency, coverage version and disclosures. This may reuse existing tariff calculation machinery; it may **not** silently recalculate at current tariff when the accepted version has changed.

**Approve combining separate `AcceptAsync` and `EnsureOrderAsync` into one Payment-owned, idempotent operation** executed after committed hold and outside Operations guards. Payment atomically validates/accepts the exact reviewed commercial version and creates/returns one reservation deposit order; a repeated `operationId` and same payload returns the same result, while a mismatched payload conflicts. There is no separate Quote-acceptance HTTP call required. A distinct durable quote table is an owner implementation choice; the **immutable quote/review semantics, stable version and snapshot are mandatory**. `EnsureOrder` remains a needed *logical order-provisioning operation*, even if the endpoint or internal function is renamed.

If the quote/tariff/coverage acceptance fails after Operations has already committed its hold, Operations durably releases/invalidates that unpaid hold and reports renewed review; never silently charge the new tariff. If a remote acceptance/order succeeded but its response is lost, recover by stable identity; never assume absence or create a second order. Store order expiry as exact H without extending it; no new order can be created at/after H. Genuine zero-price confirmation requires a trusted quote/coverage acceptance path without a synthetic order.

Coverage is owned by its authoritative service; Payment cannot invent pass/voucher validity. Agree the covered-version/acceptance/cancellation handshake with that owner. Missing tariff or coverage evidence fails closed rather than becoming an assumed zero price. Payment retains a safe immutable billing snapshot for future checkout and audit.

## 2. Status separation and observable contract

| State family | Example states | Owner | Meaning |
|---|---|---|---|
| Creation receipt | `PENDING_AUTHORIZATION`, `PREPARING`, proposed `HOLD_CREATED`, `DENIED`, `REJECTED`, `FAILED_DEPENDENCY` | Operations | Request processing, **not** payment or parking lifecycle |
| Reservation | `PENDING_PAYMENT`, `CONFIRMED`, `EXPIRED`, later `ALLOCATED`, `PARKING`, `COMPLETED` | Operations | Entitlement and lifecycle |
| Order/checkout | `CREATED`, `UNKNOWN`, `CLOSED_NO_ORDER`; checkout attempt PENDING/FAILED/UNKNOWN | Payment | Whether an order and attempt exist; not booking confirmation |
| Collection | Each verified collection ID, settlement and reconciled outcome | Payment | Authoritative money movement; independent of reservation state |
| Refund | Requested/accepted, pending, paid/failed | Payment with approved business authority | An accepted refund request is not payout proof |

The own reservation read returns nullable `orderId`, accepted values and H. A hold event may arrive before order readiness; the frontend waits without manufacturing a QR or assuming the slot is confirmed. A user refresh resumes the same reservation request/booking and order identity; never creates a new charge from an ambiguous response.

## 3. Proposed integration contracts — wire/schema agreement still required

**Payment S2S order operation (proposal)**: `PUT /internal/reservation-deposit-orders/{operationId}` using a dedicated authenticated Operations machine identity. Request binds `operationId`, `reservationId`, `accountId`, `tenantId`, `siteId`, selected vehicle/slot/UTC interval and method, accepted quote/terms identity/version/expiry, booked amount, deposit amount, currency, coverage reference/version (when applicable), and exact `expiresAt=H`. Payment checks that all price inputs match its immutable accepted offer; Operations never supplies an unaudited arbitrary amount. Keep identical-request replay, mismatch `409`, `CREATED(orderId)`, `UNKNOWN`, and durable `CLOSED_NO_ORDER` semantics. `now >= H` forbids new orders; already-created orders must remain discoverable by replay. An unknown/lost HTTP result is not absence.

**Checkout (reuse)**: Authenticated Driver invokes Payment's existing `POST /api/v1/payments/orders/{orderId}/checkout` on an authorized order. Payment must enforce Account ownership, expiry at H (including equality), attempt idempotency, stable provider key, actual payOS response replay, and UNKNOWN reconciliation before another charge. Operations does not proxy checkout unless a separate decision requires it.

**Verified money event (proposed new versioned contract)**: `ReservationDepositCollectedV1` with `eventId`, `collectionId`, `paymentOrderId`, `paymentTransactionId`, `operationId`, `reservationId`, `accountId`, `tenantId`, `siteId`, `quoteId`/accepted terms ID and version, `amount`, `currency`, verified provider reference/time, and `occurredAt`. Either use this dedicated event or a backward-compatible revised generic event that carries **equivalent immutable bindings**; owners must agree name, message identity, exchange, queue, ACL, schema version and replay/retention before implementing consumers. Publishing `PaymentSucceededV1` as-is is not enough to prove the original quote/version. Stage financial collection and outbox atomically; notify **every distinct collection**, including after an order/receipt is terminal. Duplicated delivery keeps the same IDs; contradictory payloads fail closed.

**Collection/reconciliation lookup (fallback only)**: Read all verified collections for one operation/order under restricted S2S auth; never return a naked paid boolean or newest-only record. Expose `PENDING`, `UNKNOWN`, `SETTLED`, and **definitive** `CLOSED_NO_COLLECTION` only with authoritative proof. Operations sends stable reconciliation acceptance keyed by reservation, collection and reason; Payment exposes durable status, and only actual refund/payout evidence means completed refund.

## 4. Required implementation and test gates

- **Operations:** narrow request workflow, introduce durable post-hold order stage and event consumer, preserve correct receipts/own reads and stable idempotency; move all owner HTTP outside guarded transactions, keep UoW atomic changes and the verified pre-T checkpoint; retain/verify the Facility confirmation dependency until the fast-path proof gate is satisfied.
- **Payment:** reuse the existing payOS provider verifier and EF outbox, harden order/checkout permissions, persist provider attempt before side effects, fix UNKNOWN handling, expiry H, immutable order/quote binding, late/additional collection events and reconciliation/refunds. A generic current-price order is not an accepted reservation deposit order.
- **Facility/Operations:** enumerate all mutation routes and prove held-claim fencing/invalidation before omitting the confirmation acquire. Include asynchronous structure change visibility, physical Gate edits, future service writers, direct SQL caveats and replay after crash. No new blanket claim of safety before tests.
- **Frontend:** show price preview and reviewed terms; submit one stable intent, wait for authoritative order, use genuine payOS checkout, resume same receipt/booking after redirect/reload, and display confirmed only when the Operations own read says `CONFIRMED`.
- **QA:** exact H and T boundaries, valid-hold fast path **after proof**, expired-H capacity reacquisition, competing claims, Facility edit and physical entry races, duplicate/out-of-order collection events, lost order/commit acknowledgments, additional money after PAID, zero price, refund-status separation, and fail-closed dependencies. Tests are **not yet run**.

## 5. Supersession and open contract details

This document supersedes old directions that require the reservation creation worker to stay live/poll Payment for five minutes, or demand a separate `commercial.AcceptAsync` HTTP round-trip. It **does not** supersede the November 10 checkpoint timestamp or the business rule allowing pre-T reacquisition of expired holds. The existing code and earlier docs are *implementation baselines*, not proof of the amended architecture.

The following are **TO AGREE among implementing owners**, not silently user-approved wire facts: exact request terminal-state token/compatibility; Payment endpoint names and response DTOs; Payment event type/version and MassTransit topology/ACLs; machine-auth token issuer/audience/permission; quote persistence strategy; authoritative coverage acceptance protocol; precise Facility claim-invalidation enforcement and a verified gate-removal plan. These choices must honor every approved invariant above and the project [coding skill](../../07-skills/SmartPark-Coding-Skill-v1.0.md).
