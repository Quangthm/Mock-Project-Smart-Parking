# SPARK-273 E2-01 — historical payment-return frontend handoff

26 November 2026, Asia/Saigon. Baseline `3dc3a57322158507a59c4028851f0b4cecc7d162` on `feature/SPARK-273-driver-book-parking-ahead-of-arrival`. This is a frontend-owner handoff only. No frontend production code or backend identifier was changed in Checkpoint E.

## Defect and exact source

- `FE/src/roles/driver/payments/PaymentResultPage.tsx:60-68` resolves the Operations request identity from `PaymentApi.orderReservation(boundOrder)`. For a `HISTORICAL` order it calls `reservationsApi.requestForOrder(boundOrder)` and stores only that request ID, discarding the reservation ID returned by Payment.
- `FE/src/roles/driver/payments/PaymentResultPage.tsx:51-54` then selects the progress snapshot only when `progress.snapshot?.reservationId === requestId`.
- `FE/src/roles/driver/payments/PaymentResultPage.tsx:170-177` treats a successful payment without that snapshot as "Payment received. Awaiting reservation confirmation."

Payment authoritatively distinguishes the two identities: `src/Services/PaymentService/SmartParking.PaymentService.Application/Reservations/ReadReservationCheckoutHandler.cs:21` returns `HISTORICAL` with `ReservationId = order.ReferenceId`. The Operations lookup is a different fact: `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReadOwnOrderRequestHandler.cs:8-12` calls `FindOwnByOrderAsync`, and `src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/ReservationRequestRepository.cs:35-39` returns the booking's `RequestId`, not its `ReservationId`.

## Root cause and concrete example

The reviewed reservation path uses equal request and reservation identities, but a historical order can legitimately carry different ones. The page compares a reservation identity to a request identity and therefore discards an otherwise authorized, committed snapshot.

Example with synthetic UUIDs:

- Payment `orderReservation` returns `state = HISTORICAL`, `reservationId = 22222222-2222-4222-8222-222222222222`, `orderId = order bound from the URL`.
- The authenticated Operations lookup returns `requestId = 11111111-1111-4111-8111-111111111111`.
- The scoped progress snapshot has `requestId = 11111111-1111-4111-8111-111111111111` and `reservationId = 22222222-2222-4222-8222-222222222222`.
- Current code compares `22222222-...` with `11111111-...`, discards the snapshot, and can keep displaying "Payment received. Awaiting reservation confirmation." even though the reservation is confirmed.

## Customer-visible impact

A Driver returning from a genuine historical payment may see a successful payment paired with a false pending-confirmation message. The booking is actually committed and readable from authoritative sources. This affects historical-return support during a populated cutover; the current reviewed booking path uses equal IDs and is not the demonstrated failing case. Do not describe this as a backend confirmation defect.

## Recommended correction

Keep both identities and select the snapshot by the fact each identity represents:

1. Preserve Payment's authoritative `reservationId` from `orderReservation` alongside the Operations `requestId` from `requestForOrder`; do not overwrite one with the other.
2. Subscribe progress by the Operations request ID, then match the snapshot's `reservationId` against Payment's reservation ID for `HISTORICAL` results. For reviewed results where the IDs are equal, the same logic continues to pass.
3. Continue rejecting a snapshot whose reservation identity does not match the Payment-bound reservation identity. Do not simply remove identity validation.
4. Continue checking that the Operations lookup belongs to the same `orderId`, that Payment returned the same order as the URL, and that the authenticated owner is the actor for both reads.
5. Preserve the existing `generation`/`active` stale-response guard, the progress revision fence, the initial authoritative read, reconnect behavior, and the existing manual "Check status again" control for standalone payment status retries.
6. Keep the URL `requestId` only as a presentation hint; it must not select ownership or replace the authenticated order-bound lookups.

Suggested owner: Frontend, with Payment and Operations contract support. A separate implementation approval and a genuine paired historical fixture are required before code changes.

## Required regression cases

- Equal-ID reviewed booking: Payment reservation ID and Operations request ID match; confirmed and non-confirmed states render correctly; replay and reconnect still work.
- Distinct-ID historical booking: Payment reservation ID differs from the Operations request ID; the authorized committed snapshot is selected and renders `CONFIRMED`; a mismatched snapshot is still rejected.
- Wrong owner or wrong order: foreign and unauthenticated reads are rejected; no snapshot is rendered from a different Account or order.
- Stale responses: a late response from an old generation, old order binding or older progress revision cannot overwrite the current result.
- Payment status independence: a successful payment with no committed booking still shows the pending-confirmation state; a failed or cancelled payment does not render confirmation.
- Reconnect: dropping and restoring the SignalR connection runs the authoritative read again without interval reservation-status polling and preserves the correct identity selection.

Tester case `docs/04-testing/reports/SPARK-273-checkpoint-e-tester-handoff.md` R02 already records the distinct-ID historical-return branch as pending. Keep this defect separate from the UserService test-project blocker and from Checkpoint D durability verification.
