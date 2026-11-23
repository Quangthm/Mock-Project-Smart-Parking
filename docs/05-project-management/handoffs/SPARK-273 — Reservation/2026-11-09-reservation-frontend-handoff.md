# SPARK-273 Driver frontend handoff

Use this handoff for the reviewed CAR booking and payment cutover. The backend supports quote review, guarded admission, linked Payment order, payOS checkout and verified reservation confirmation. The checked-in browser review still calls `/api/reservation-terms` with the VehicleId/terms contract. The project lead's local manual Postman run passed through **05.1**, with an active `PENDING_PAYMENT` reservation and a genuine linked unpaid order. Reviewed browser and live merchant acceptance are pending. [Current client](../../../FE/src/lib/reservationsApi.ts) · [Postman guide and result](../../04-testing/postman/SPARK-273-manual-test-guide.md#6-optional-cases-and-verification)

## Review and submit

1. Use the authenticated Driver's registered CAR and its `canonicalPlate` from `GET /api/vehicles`. Select an eligible Facility slot and a policy-valid future interval. The slot identifies the authoritative tenant and site.
2. Send `POST /api/reservation-quotes` with `licensePlate`, `requestedSlotId`, `start`, `end` and `paymentMethod`. Show the returned selection, price, deposit, currency, disclosure and quote expiry. Require explicit acceptance of those exact terms. Re-review when the selection changes or the quote expires before guarded acceptance.
3. Send `POST /api/reservation-requests` with the reviewed selection, `quoteId` and `quoteVersion`, and a UUID `Idempotency-Key` header. Persist the exact body and key before sending. A 202 v3 receipt supplies `requestId`. Recover an uncertain submit with the same authenticated Account, body and key. A changed intent needs a new review and key once the earlier outcome is known. [Reservation API controller](../../../src/Services/OperationService/SmartParking.OperationService.API/Controllers/ReservationRequestsController.cs)

## Progress and recovery

Subscribe to `/hubs/reservation-progress` with `Subscribe(requestId)`, then read `GET /api/reservation-requests/{requestId}/progress` once after the initial subscription or restored subscription. Reuse the [reservation progress client](../../../FE/src/lib/reservationProgress.ts). Apply only newer revisions for the current request and session. Progress reports the Account, vehicle, Facility and booking checks. A terminal denial or dependency failure uses the recorded reason. Render committed snapshots and use countdown timers for display. A manual recovery read can use the same scoped request or booking endpoint.

## Checkout and confirmation

- Wait for the own-booking read to show `PENDING_PAYMENT`, `orderProvisioningState = CREATED`, a real `orderId` and a future `holdExpiresAt` (H). Until then, show preparation or its recorded outcome.
- Use the linked order for `POST /api/v1/payments/orders/{orderId}/checkout`. Render the returned payOS checkout URL or QR. On payment return, restore the request using `GET /api/me/reservations/by-order/{orderId}/request` and resume the scoped progress flow. A URL request hint should be checked against the order binding.
- Render `CONFIRMED` from the Operations reservation state. Payment owns the verified collection. Operations owns entitlement, including the guarded outcome after late money. Surface expiry and reconciliation independently from a successful provider redirect. [Payment checkout API](../../../src/Services/PaymentService/SmartParking.PaymentService.API/Controllers/PaymentsController.cs) · [Payment authority](2026-11-19-SPARK-273-payment-authority.md)

## Browser acceptance and legacy cutover

- In the approved test stack, verify quote acceptance, lost-response replay, account and request scope, SignalR reconnect, and the payment readiness gate.
- With the approved merchant fixture, collect evidence for checkout, callback, Payment settlement, and committed Operations confirmation. Exercise duplicate delivery and payment near hold expiry. Keep shared parking-session checkout and legacy return paths in the regression scope.
- Enable the reviewed browser flow for new bookings after acceptance. Inventory old clients and outstanding work before retiring legacy writes. Preserve compatible reads, original idempotency data, and payment recovery during the drain. `06 — Zero Amount / Subscription Coverage` remains blocked on a genuine entitlement source. [Owner and rollout handoff](2026-11-18-SPARK-273-reservation-owner-handoffs.md#rollout-and-legacy-retirement) · [payOS acceptance plan](../../04-testing/payment-integration/payos-test-plan.md)

Historical frontend decisions and prior implementation evidence are available in the [original frontend handoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-09-reservation-frontend-handoff.md).
