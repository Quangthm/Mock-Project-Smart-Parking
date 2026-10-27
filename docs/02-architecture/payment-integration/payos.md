# payOS integration — RC3 implementation clarification

**Basis:** approved [PAY-01](../../05-project-management/decision-log/2026-10-13-payos-priority-pay-01.md). Provider implementation and tests are pending. This document translates existing rules into design obligations; proposed field names and SDK choices are not frozen API/schema contracts.

## Provider and channel identity

| Concept | Required distinction |
|---|---|
| Online route | payOS QR is the first implementation priority. |
| Processing provider | Record payOS for a payOS transaction, even if scanned with another app. |
| Payer application | Separate optional evidence; do not infer from selected UI label or claim universal wallet compatibility. |
| Native VNPay/MoMo/ZaloPay | Separate adapters/options. Existing working implementations may remain; unfinished ones are explicitly simulated/deferred. |
| Environment | payOS production with real funds versus isolated deterministic fixtures; do not label production as sandbox. |
| Cash | Assigned-Operator audited CASH_COLLECT; unchanged role and reconciliation requirements. |

A native-provider button must not silently route to payOS. Demo-only simulations must be visibly labelled and unable to mutate real-money balances. Real-money checkout exposes functioning routes. Route selection never relaxes authentication, scope or amount validation.

## Service and Clean Architecture placement

Payment & Pricing owns the adapter, order/attempt identities, accepted amounts, transaction reconciliation, refunds and invoices. Domain contains financial invariants without payOS SDK types. Application orchestrates use cases through provider interfaces. Infrastructure contains payOS HTTP/SDK/signature handling. Persistence stores ledger, logical operations, inbox/outbox and provider correlations. API validates/authenticates requests and receives provider callbacks through the trusted verification boundary. Never place provider secrets or authoritative payment verification in React.

Operations owns reservations, capacity claims, admission and session occupancy. Verified money produces a durable fact consumed idempotently by Operations; it is not permission to update Operations tables. Preserve RabbitMQ/MassTransit outbox/inbox contracts. Notification failure does not roll back a settled payment. No additional microservice is required solely for payOS.

## Creation and confirmation

1. Resolve authorization and immutable accepted charge; genuine zero-price confirmation bypasses collection with ordinary domain guards.
2. Persist one logical operation/attempt and stable provider order correlation before the external request. Choose orderCode representation and uniqueness during schema review; never generate a new order simply because the caller retried.
3. Create the payment link with accepted VND amount, configured recipient, permitted return/cancel URLs and appropriate expiry. Provider expiry does not redefine the domain hold deadline.
4. A timeout after creation is UNKNOWN. Query the same operation; resume the existing link when established. Only definitive retryable failure permits a fresh attempt under the existing four-total-attempt limit. API rate limiting/redelivery does not automatically imply payment failure.
5. Verify provider signature and expected order, amount, currency, payee and transaction reference on callback/query. Never trust client success, screenshots, QR generation or provider connectivity-test sample data as payment proof.
6. Durably record/deduplicate accepted evidence and publish through the outbox; acknowledge callbacks consistently with durable ingestion. Delivery/reconciliation can replay without re-crediting.
7. On missing/delayed callbacks, authenticated provider lookup reconciles the same payment. A later failure/cancel indication must not erase verified collected money. Mismatched/partial/excess/unmatched funds require financial reconciliation, not fabricated full settlement or discarded money.

Signature verification and provider states must be checked against the actual API/SDK version. Exact retry schedule, timeouts and webhook response/retry behavior remain implementation validation, not invented business policy.

## Money, deadlines and races

| Situation | Required SmartPark outcome |
|---|---|
| Duplicate callback/query result | Credit once by stable provider transaction identity; no duplicate invoice, refund or right. |
| Browser cancel or link cancellation races money | Reconcile actual money independently; link cancellation is neither proof of no money nor a refund. |
| Ordinary payment after five-minute hold expiry | Operations atomically reacquires compatible capacity if free; otherwise reconciliation/refund. No displacement or conflicting resurrection. |
| Monthly money arrives but entitlement commit is at/after H | No activation; reconcile/refund under strict A<H, including the domain commit. |
| Payment UNKNOWN and user asks to pay cash | Resolve same operation first; no blind second collection. |
| Duplicate real transfer | Account for distinct money separately; never grant a second entitlement accidentally. |
| Charge covered; surplus refund pending | Commercial checkout can finish; payout remains visible and separate. Physical occupancy is released only by actual departure. |

## Refund capability checkpoint

The approved 300,000 VND collection / 125,000 VND charge still produces one 175,000 VND surplus refund obligation. Reserve eligible refundable balance atomically, including pending/UNKNOWN refunds, and credit prepayment once. Approval or payment-link cancellation is not payout completion.

Validate whether the selected payOS/bank arrangement supports the approved original-route refund, how the original payer/recipient is reliably correlated, and how partial refunds, transaction identifiers, retries and actual payout proof work. The published separate payout service is a candidate requiring verification; it is not automatically equivalent to reversing the original collection. Do not assume complete payer-account details are available for every bank.

If unsupported, keep the refund pending with a visible integration/reconciliation issue and escalate the concrete gap. No silent cash fallback for an original online payment and no new manual bank-transfer refund workflow are authorized here. Original cash returns remain audited cash returns. Fixtures prove internal behavior only; they do not prove delivery of real refunds.

## Technical decisions to record before execution

Record actual API/SDK version, merchant account and lot-to-payee mapping, secret storage/rotation, order correlation, reachable HTTPS callback configuration, provider/bank limits, supported payer apps, reconciliation scheduling and test account/custody/budget. These are implementation choices; any change to financial recipient authority, refund method or product limits returns to PM. Do not invent new universal deposit caps from provider technical limits; validate representability explicitly.

No real funds are transferred by this documentation task. Run controlled payment tests separately using the [test plan](../../04-testing/payment-integration/payos-test-plan.md). Production money paths must remain separate from local load/fault fixtures.

## Official references

Checked 13 October 2026: [Checkout](https://payos.vn/docs/checkout/how-checkout-works/), [test environment](https://payos.vn/docs/moi-truong-test/), [API](https://payos.vn/docs/api/), [signature FAQ](https://payos.vn/docs/cau-hoi-thuong-gap/), [payout offering](https://payos.vn/ra-mat-kenh-chi-tren-payos-giai-phap-chi-tu-dong-qua-api/). Provider capabilities remain subject to verification against the selected account and deployed API version.
