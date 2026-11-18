# SPARK-273 — PaymentService confirmation owner handoff

**Scope:** Slice 4 paid-deposit and future zero-amount acceptance contracts. Slice 2 provides the immutable plate-bound quote. Source reviewed at `594ee7f10ef642467b70e9eaa6d5dd5d772499ed`. Runtime verification pending.

## Paid deposits — open event contract gate

The [11 November D11-B decision and verified-money contract](../decision-log/2026-11-11-reservation-event-driven-confirmation.md) require a durable authenticated versioned event for each distinct verified collection, including additional or late money after a terminal order or reservation receipt. Bind stable event and collection identities, order/transaction and operation IDs, reservation, Account/tenant/site, immutable Payment quote ID/version, amount/currency and verified provider reference/time. Deduplicate effects by financial identity. Keep parking-session routing independent.

| Source | Finding |
| --- | --- |
| `PaymentSucceededV1`, `PaymentSucceededPublisher` and `ReservationPaymentSucceededConsumer` | Generic event carries transaction/order, reservation reference, scope, amount/currency and provider data. It lacks explicit collection identity and quote ID/version. The publisher generates an event ID at publication. |
| `RecordReservationPaymentHandler` | Checks order, scope, deposit and currency against the Operations booking, then reuses transaction ID as collection identity and copies quote fields from the booking. This establishes local consistency. Payment must prove one stable identity per distinct collection and independently attest the original quote binding. |
| `GetReservationCollectionsQueryHandler` via `GET /internal/reservation-deposit-orders/{orderId}/collections` | Enumerates successful transactions by order, but returns an empty QuoteId, placeholder terms version and literal VND. `ReservationCommercialClient.CollectionsAsync` rejects the empty QuoteId. Current lookup cannot supply equivalent evidence. |
| `EnsureReservationDepositOrderCommandHandler` | Persists order/quote binding, amount and H. Its replay helper echoes caller currency and method. Current order replay cannot independently establish the full binding. |

**Release gate:** Payment and Operations must agree the versioned event and targeted recovery lookup. A dedicated event such as proposed `ReservationDepositCollectedV1`, or a compatible generic revision with equivalent immutable evidence, satisfies the approved direction. A generic signal plus a complete authoritative Payment lookup would require explicit approval as an equivalent contract, because D11-B expects equivalent event-level evidence and defines lookup for recovery. Prove stability of collection IDs, full quote/order binding, durable publication for every distinct collection, late replay, transport compatibility and ACLs before cutover. Current generic wiring remains an open exception.

Test duplicate, reordered and contradictory evidence, multiple collections even after terminal status, exact H, lost order acknowledgement, inaccessible lookup and late-money disposition. Retain paid pre-T reacquisition only where the existing guards permit it. Preserve reconciliation even when entitlement cannot be granted.

## Future zero-amount acceptance — HTTP response

The Operations transactional outbox delivers a durable MassTransit trigger to its consumer. The consumer calls a Payment-owned authenticated idempotent HTTP operation with stable operation/reservation identity, original Account, canonical plate, tenant/site/slot, interval/method, quote and coverage versions, currency, exact H and zero due. Coverage follows the plate. Account context governs purchase access, billing and audit. Agree the exact route, wire and persistence with Payment.

Payment records or replays one genuine linked zero-value order/reference, authoritative acceptance and a durable receipt/invoice record or issuance obligation. Return stable IDs and immutable evidence. Use no provider attempt, synthetic collection or second Payment acceptance event. Receipt rendering/download proceeds separately.

Recover ambiguous responses with exact replay or agreed authoritative lookup. Conflicting replays fail. Fence new acceptance at H while allowing recovery of committed evidence. Operations requires matching evidence and a still-protected claim at its guarded checkpoint strictly before H. If expiry wins, release and require fresh review and a new request. Later evidence is for reconciliation. Verify durable recovery before retiring the current deferred handler/worker path. See the [Operations recovery matrix](2026-11-18-SPARK-273-reservation-owner-handoffs.md#covered-acceptance-recovery).
