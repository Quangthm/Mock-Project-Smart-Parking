# SPARK-273 reservation flow — Postman manual verification

The collection exercises the current Operations routes and uses the existing Payment checkout/status routes only when Operations has returned a real Payment order. It never creates a local or demo order to make the flow appear successful.

## Artifacts

| File | Purpose |
|---|---|
| [SPARK-273-Reservation-Flow.postman_collection.json](./SPARK-273-Reservation-Flow.postman_collection.json) | Collection v2.1: price preview, reviewed terms, idempotent request, receipt and own-reservation reads, conditional Payment checkout/status, and negative checks |
| [SPARK-273-Local.postman_environment.example.json](./SPARK-273-Local.postman_environment.example.json) | Blank local variables; tokens are empty and must not be committed |

## Prerequisites and current integration gate

- Start the local PostgreSQL, RabbitMQ and service dependencies using [LOCAL-SETUP.md](../../../LOCAL-SETUP.md). The Operations API defaults to `http://localhost:5055`; PaymentService defaults to `http://localhost:5065`.
- Enable reservation admission and the Operations transactional outbox with the environment settings documented in the deployment checklist. Do not apply the changed original schema script to an existing/shared database without the deployment owner's schema inventory and rollout approval.
- Use a real active Driver token and an Account-owned eligible CAR vehicle. Populate an active site and a compatible specific slot with the required capacity/policy rows.
- Operations now uses a real Payment HTTP adapter for preview, quote and post-hold deposit-order provisioning. Configure Operations `Services:Payment` (default `http://localhost:5065/`) and a non-empty `Services:Key`. In Development, Payment accepts that value as `X-Internal-Service-Key`; this shortcut is not production authentication. If Payment is unreachable, preview/review correctly return `503 OWNER_UNAVAILABLE`.
- The active paid-confirmation consumer uses generic `PaymentSucceededV1` on a reservation-specific queue with `ReferenceType=RESERVATION`, validated against Operations' persisted accepted-order snapshot. `ReservationDepositCollectedV1` remains source-only with no producer. This is an open event-contract deviation requiring owner review. Runtime broker/replay verification remains required.
- The new hold terminal receipt is `state=HOLD_CREATED` with `contractVersion=2`. A `PENDING_PAYMENT` reservation is not a confirmed entitlement.
- Zero-price acceptance, quote-bound collection lookup and reconciliation remain fail-closed. Do not submit a Payment status response, redirect or broker message as proof of Operations confirmation.

## Import and configure

1. Import the collection and the example environment into Postman.
2. Select **SPARK-273 Reservation Flow - local**.
3. Set `driverToken` to a local Driver token. Set `paymentDriverToken` only when Payment accepts the same Driver identity for reservation orders.
4. Set `siteId`, `slotId`, `vehicleId`, `start` and `end` from real test data. Use UTC ISO-8601 timestamps with `start` in the future and `end > start`. Set `differentEnd` to another valid interval end for the idempotency mismatch test.
5. Keep `paymentMethod=PAYOS`; configure `paymentReturnUrl` and `paymentCancelUrl` only with safe local callback URLs.
6. Never export populated secret values. The committed environment contains no token or password.

## Run sequence

Run requests in collection order, one at a time for the first pass:

1. **01.1 Server price preview** and **01.2 Review and capture exact terms**. The post-response scripts save quote ID/version, terms ID/version, expiry and accepted amounts.
2. **01.3 Create reservation** uses one generated `Idempotency-Key`. It saves the request ID and, when present, reservation ID. Expected initial response is HTTP 202.
3. **01.4 Replay same create** must return the same request ID. **03.1 Same key with changed intent** must return HTTP 409.
4. **02.1 Read creation receipt** until its state is terminal. A committed hold returns `HOLD_CREATED` (contract version 2); this is not `CONFIRMED`.
5. **02.2 Read own reservation** captures `state`, exact `holdExpiresAt`, `orderId`, order-provisioning state and zero-price acceptance state. `PENDING_PAYMENT` is expected after hold commit.
6. If and only if `paymentOrderId` is nonempty and came from the Operations read, **02.3 Read Payment invoice** and **02.4 Initiate real Payment checkout** become eligible. The checkout uses Payment's actual `POST /api/v1/payments/orders/{orderId}/checkout` endpoint and an idempotency key. The pre-request script skips it when no real order ID exists.
7. When checkout returns a real `paymentTransactionId`, **02.5 Read Payment transaction status** uses Payment's actual status endpoint. This status is diagnostic only.
8. Re-run **02.6 Re-read reservation**. Mark the reservation confirmed only when this Operations response reports `state=CONFIRMED`; a Payment `SUCCESS`, redirect, QR scan or checkout callback is not confirmation.
9. **03.2 Review overlapping reservation terms**, **03.3 Submit overlapping request** and **03.4 Read competing request conflict receipt** use a second `conflictVehicleId` owned by the Driver, the same specific site/slot, and a time window in conflict with the original interval. Set `conflictStart`/`conflictEnd` accordingly. Expect the competing request to become `REJECTED` with `SLOT_TIME_CONFLICT`; rerun 03.4 if it is still pending. This tests slot capacity independently of a same-plate conflict.
10. **03.5 Re-read reservation after hold deadline** checks Operations' current state against the captured exact H. Run it at least five seconds after H and expect `EXPIRED` unless the reservation was independently confirmed.
11. **03.6 Anonymous own-reservation read** must return HTTP 401.

The scripts capture IDs and state in the selected Postman environment so a later request or refresh reuses the same reservation rather than creating another one.

## Recovery, conflict and expiry checks

- **Creation replay:** rerun 01.3 with the saved key and identical body; the same receipt/request must be returned. Do not generate a new key after a timeout until the existing receipt has been read.
- **Changed replay:** run 03.1 with `differentEnd` set; expect 409 `IDEMPOTENCY_KEY_REUSED`.
- **Capacity conflict:** with the current adapters configured, prepare a second eligible vehicle and review its terms for the same specific slot and overlapping interval. Submit with a new idempotency key. Expect the authoritative Operations conflict code in its receipt and no second active hold. If the second account is used, obtain its own token and terms; do not copy another Account's terms.
- **Hold expiry:** save `holdExpiresAt`, wait past H, then read the own reservation. The expiry worker should release the temporary claim and Operations should report `EXPIRED`. Verify no new checkout/order can be created at or after H. For an already-created order or late collection, use the Payment/Operations recovery runbook; never infer a refund payout from an accepted reconciliation request.
- **Stale terms:** change/expire the reviewed tariff or coverage between review and order provisioning. The old snapshot must not be repriced; expect durable `CLOSED_NO_ORDER`, released hold and renewed review from the live adapter.
- **Zero price:** only use a Payment-owner-issued, versioned free/covered quote. Verify `zeroPriceAcceptanceState=ACCEPTED`, no `orderId`, no synthetic payment, and guarded Operations `CONFIRMED` after the pre-T evaluation. This case is blocked because Payment has no zero-price acceptance endpoint.
- **Expired-hold payment recovery:** the active consumer handles generic `PaymentSucceededV1`, but quote-bound collection lookup and reconciliation are unavailable. Deliver verified money after H but before T only in an approved test environment; confirm only if the guarded Facility/Operations reacquisition succeeds. Otherwise reconciliation remains blocked and must be reported, not simulated. At/after T, expect no new confirmation.
- **Duplicate delivery:** redeliver the same payment event/transaction identity; expect one durable payment-evidence path, one confirmation effect, and no duplicate confirmation outbox fact. The endpoint has no general EF Inbox; verify business idempotency under the locked reservation.

## Expected current result / blocked scenarios

With the checked-in source and correctly configured local services, preview/review should reach Payment and the reservation should reach `HOLD_CREATED`/`PENDING_PAYMENT` with a real Payment order when provisioning succeeds. `503 OWNER_UNAVAILABLE` from preview/review now means Payment is absent/unreachable or `Services:Payment`/`Services:Key` is not configured; it is not the unconditional expected result. `503 OWNER_CONTRACT_INVALID` means an owner envelope/echo/binding check failed.

Zero-price acceptance, quote-bound collection lookup, reconciliation and production machine authentication remain **BLOCKED**. Event-driven paid confirmation uses generic `PaymentSucceededV1` and is source-integrated but not live-verified; the event-schema deviation needs owner review. PostgreSQL/RabbitMQ/provider/replay behavior and this Postman run remain **NOT RUN — USER VERIFICATION REQUIRED**. The Payment requests are intentionally conditional and will skip when Operations has not supplied a real order or Payment has not supplied a transaction ID.
