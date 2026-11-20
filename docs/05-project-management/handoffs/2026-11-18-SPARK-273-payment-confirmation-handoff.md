# SPARK-273 — PaymentService confirmation owner handoff

**19 November owner decision.** Use the existing order PUT for paid and zero amounts. Use PaymentSucceededV1 and authenticated order-scoped lookup for paid collections. See [Payment authority and paired verification](2026-11-19-SPARK-273-payment-authority.md).

## Paid deposits and historical source findings

The 19 November commercial decision approves a shared success event as the wake-up and a versioned authoritative lookup as the financial proof. The table records the gaps observed before this implementation.

| Source | Finding |
| --- | --- |
| `PaymentSucceededV1`, `PaymentSucceededPublisher` and `ReservationPaymentSucceededConsumer` | Generic event carries transaction/order, reservation reference, scope, amount/currency and provider data. It lacks explicit collection identity and quote ID/version. The publisher generates an event ID at publication. |
| `RecordReservationPaymentHandler` | Checks order, scope, deposit and currency against the Operations booking, then reuses transaction ID as collection identity and copies quote fields from the booking. This establishes local consistency. Payment must prove one stable identity per distinct collection and independently attest the original quote binding. |
| `GetReservationCollectionsQueryHandler` via `GET /internal/reservation-deposit-orders/{orderId}/collections` | Enumerates successful transactions by order, but returns an empty QuoteId, placeholder terms version and literal VND. `ReservationCommercialClient.CollectionsAsync` rejects the empty QuoteId. Current lookup cannot supply equivalent evidence. |
| `EnsureReservationDepositOrderCommandHandler` | Persists order/quote binding, amount and H. Its replay helper echoes caller currency and method. Current order replay cannot independently establish the full binding. |

**Release gate.** The earlier dedicated collection-event proposal is deprecated. Keep the existing PaymentSucceededV1 fields and topology. Verify the paired consumer, credentials, provider and existing-data rollout before activation.

Test duplicate, reordered and contradictory evidence, multiple collections even after terminal status, exact H, lost order acknowledgement, inaccessible lookup and late-money disposition. Retain paid pre-T reacquisition only where the existing guards permit it. Preserve reconciliation even when entitlement cannot be granted.

## Zero-amount acceptance through the order PUT

The Operations transactional outbox delivers a durable MassTransit trigger to its consumer. The consumer calls a Payment-owned authenticated idempotent HTTP operation with stable operation/reservation identity, original Account, canonical plate, tenant/site/slot, interval/method, quote and coverage versions, currency, exact H and zero due. Coverage follows the plate. Account context governs purchase access, billing and audit. Payment accepts zeroSelection on the same authenticated order PUT. It returns the genuine order and invoice IDs in one response.

Payment records or replays one genuine linked zero-value order/reference, authoritative acceptance and a durable receipt/invoice record or issuance obligation. Return stable IDs and immutable evidence. Use no provider attempt, synthetic collection or second Payment acceptance event. Receipt rendering/download proceeds separately.

Recover ambiguous responses with exact replay or agreed authoritative lookup. Conflicting replays fail. Fence new acceptance at H while allowing recovery of committed evidence. Operations requires matching evidence and a still-protected claim at its guarded checkpoint strictly before H. If expiry wins, release and require fresh review and a new request. Later evidence is for reconciliation. Verify durable recovery before retiring the current deferred handler/worker path. See the [Operations recovery matrix](2026-11-18-SPARK-273-reservation-owner-handoffs.md#covered-acceptance-recovery).
