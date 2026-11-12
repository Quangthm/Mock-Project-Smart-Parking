# Reservation first slice — frontend handoff and readiness

> **SPARK-273 architecture alignment (11 November 2026).** This document includes historical implementation notes. For the current approved workflow and confirmation boundary, see the [11 November decision](../decision-log/2026-11-11-reservation-event-driven-confirmation.md). Existing Facility confirmation checks remain required until the aggregate-only fast-path invariant gate is verified.

Reviewed 9 November 2026 in the local SPARK-273 checkout. Source inspection only: no frontend changes, frontend build, application startup, live API test or Docker publish verification was performed by the agent. The user subsequently reported a successful first-slice backend build and deferred tests until the booking workflow is ready. The resumed scope is booking through confirmed reservation, with producer implementations retained by teammates. This is a backlog for frontend/backend teammates, not a claim that bookings work. See the [continuation design and unresolved decisions](../../02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md#booking-through-confirmation-continuation-review--9-november).

## What can be tested now

Once deployed with aligned schemas, credentials and messaging, Postman or another HTTP client can exercise the existing **admission and receipt**, not a valid confirmed booking. The user has chosen to defer this testing until the next phase is implemented:

1. Sign in as a real active Driver and obtain its JWT.
2. POST `/api/reservation-requests` with a UUID `Idempotency-Key` and the documented intent body.
3. GET the returned receipt Location using the same Account.
4. Observe pending authorization, denial or timeout. Allowed now proceeds to `PREPARING` in the source draft and persists the one-time vehicle binding. Missing owner integration keeps valid booking progression gated; expired/invalid terms can reject the request. The old `RESERVATION_FINALIZATION_NOT_READY` terminal behavior is superseded.

Admission alone does not prove site/slot capacity, quote issuance or commercial acceptance. Preview and review routes now exist in source but return unavailable with the default missing producer adapters. Do not fabricate terms to present a valid booking. Current source has not passed a fresh build or deployment verification. See the [producer handoff and remaining Operations work](2026-11-09-reservation-producer-handoff.md).

The current frontend does not submit/poll these requests. Postman is a convenient diagnostic client, not an architectural prerequisite. Once backend behavior and frontend integration are implemented, the same flow can be tested in the browser. Changing test client cannot bypass the finalization gate.

See [API implementation/proposal status](../../03-design/api/reservation-first-slice-api-design.md) and [exact deployment checks](../../04-testing/reports/SPARK-273-deployment-checklist.md).

## Observed frontend state

| File | Current behavior | Consequence |
|---|---|---|
| `FE/src/roles/driver/booking/BookingFlow.tsx` | Loads registered vehicles through vehiclesApi and filters CAR; reads lots/slot status through driverData/store; estimates amount with browser duration and lot hourly rate | Vehicle API integration does not establish reservation or quote integration. Local slot availability and displayed amount are not authoritative. |
| Same file | `continueToPaymentStep` / `continueToPayment` switch local UI steps; no reservation-requests or reservation-terms call | Reaching payment is not durable admission or a capacity hold. |
| `FE/src/roles/driver/payments/PaymentCheckout.tsx` | Has an existing-order Payment integration; booking embeds it with `showMethodPicker=false`, displaying unavailable until a real order is supplied | Do not use booking ID as order ID, invent an order or show paid as confirmed reservation. |
| `FE/src/roles/driver/booking/ActiveBookings.tsx`, `FE/src/roles/driver/history/BookingHistory.tsx` | Read bookings via driverData/store and local change events | These views do not show authoritative Operations receipts/bookings. |
| `FE/src/roles/driver/data/data.ts`, `FE/src/lib/store.ts` | Expose legacy/local booking and lot operations | Do not use local writes to simulate successful production bookings, claims, audit or payment. |
| `FE/src/lib/authApi.ts` | Existing token-aware request helper and session generation handling | Reuse the established authentication/error flow; suppress stale responses after logout or Account change. |

## Backlog for teammates

| Priority / owner | Work | Completion condition / dependency |
|---|---|---|
| P0 / Operations + Facility + User | Complete site/slot eligibility and guarded plate/resource/quota acceptance; binding lookup now returns canonical plate | Cross-site plate identity includes guest visits. Binding is persisted once per workflow; local browser state never decides capacity. |
| P0 / Payment + coverage owners | Implement the published versioned quote and coordinated coverage producer contracts | Review/acceptance uses real versions and financial terms; no missing-price-to-zero fallback. |
| P0 / Operations + QA | Verify final guarded evaluation checkpoint and atomic confirmation | Evaluation must be before T; commit may finish later. Both paid and zero-price paths require durable commit before exposure. This explicitly approved rule replaces commit-time evidence. |
| P0 / Operations | Complete booking-through-confirmation, hold expiry and late/additional collection reconciliation | Allocation/recovery, scan, cancellation and checkout are next phase. Unavailable adapters and an incomplete capacity evaluator still block live booking. |
| P0 / API owners + frontend | Integrate current own reads at `/api/me/reservations` and `/{id}` | These avoid the Operator route. Current summary fields/pagination are in API design; cancellation remains proposed. |
| P1 / frontend | Add an Operations API adapter for request admission and own receipt reads using existing auth conventions and configured base URL | Stable per-intent idempotency key, same-key retry after ambiguous network outcome, receipt resumption after refresh, Account-scoped isolation. No bearer/key secrets persisted in ordinary UI state. |
| P1 / frontend | Separate request progress from booking lifecycle | 202 means received; `BOOKING` is not confirmed. Display pending/denied/unavailable honestly; do not create a new intent automatically after an ambiguous result. |
| P1 / frontend, after producer readiness | Add terms review and stale-terms recovery | Display server-issued versioned quote, policy values and deadlines. Changed terms require renewed review. No business defaults/horizon/hold expiry invented in browser. |
| P1 / frontend, after booking/order readiness | Pass the actual accepted order into Payment flow | Handle payment pending/unknown/late collection separately from reservation confirmation. No cash or QR shortcut creates entitlement. |
| P1 / frontend, after own-read readiness | Replace local booking sources in ActiveBookings/history/detail | Pending receipts may exist without bookings. Display requested vs assigned/observed resource; payment/refund, arrival, parking and departure remain distinct. |
| P1 / frontend, after cancellation readiness | Add authorized own cancellation and earned facility-failure right | Use server eligibility. Recovery/arrival does not erase an earned right; actual parking/unused closure ends it. No frontend-generated refunds or audit logs. |
| P2 / QA | Browser integration and recovery acceptance | Verify double-click, refresh, Account switch, 401/403/404/409/503, stale terms, outages, late replies and independent payment outcome. Confirm actual persisted state; local cards alone are not evidence. |

Frontend may build components against reviewed mock contracts while backend gates remain, but must label fixtures and must not claim real booking success. A mock client is not current backend acceptance evidence. No frontend code was changed by this handoff.

## Facility authentication alignment follow-up

At the review checkpoint, Facility had no reference to the then-proposed shared projects and no JwtBearer/JWKS middleware registration. The user has since removed those shared projects; Operations now owns its local implementation. `FacilityService.API/Program.cs` wires identity HTTP adapters; `Infrastructure/LayoutCallerResolver.cs` forwards the bearer to UserService `/api/auth/me`. Other adapters handle Owner/Operator scope. That is an existing approach, not an equivalent local JWT/JWKS implementation. A teammate's newer branch or deployment has not been inspected. Compare that actual implementation against the approved authentication/freshness contract before deciding whether to adopt, keep separate or replace anything; no Facility refactor is authorized by this handoff.

## User-confirmed happy path for the next phase

Recorded after the user requested stopping at the first-slice Git summary so they can commit before further implementation. This section is future work, not newly implemented behavior.

1. Driver is logged in.
2. Driver selects a parking lot.
3. Driver selects an available slot when supported/applicable by policy (Specific Slot first).
4. Driver completes the required vehicle, time range and payment-method inputs. Their order is interchangeable. Existing frontend layout need not change solely to reorder these inputs; all required data must be present before proceeding to payment.
5. On proceeding to payment, frontend calls backend with the complete intent. Backend response provides the authoritative request/payment progression; a browser slot click by itself does not establish a hold or confirmation.
6. After the payment interaction, frontend calls backend again to obtain/continue the authoritative finalization outcome. Confirmation is shown or redirected to only after the backend reports confirmed booking.

Future design must preserve once-per-workflow actor authorization, idempotency, producer-verified payment outcomes, server-issued terms, capacity safety and strict D4c. The second browser call/redirect alone is not trusted proof of payment. Receipt polling/recovery may be needed while asynchronous work completes; the user's two interaction points are not a ban on loading lots, checking availability or observing status.

The user resumed implementation and approved the flow below and final evaluation-checkpoint cutoff. Producer readiness, method support and capacity correctness remain release gates; do not interpret the earlier pause-at-commit instruction as still active.

## Approved client flow and examples

1. As site/slot/time change, call `POST /api/reservation-price-previews`. Display the server-calculated amount on the form, before proceeding with the selected method. Vehicle/method may be absent for this preview. Keep any existing browser calculation responsive, but refresh from authoritative pricing and discard responses for an older form selection. Never submit a browser amount as authority.
2. After vehicle, interval and method are complete, call `POST /api/reservation-terms`. Show the returned commercial and operational terms. If an updated amount differs from the earlier display, require acceptance of the updated terms. No layout change is required merely to reorder the inputs.
3. On acceptance, POST `/api/reservation-requests` with the reviewed terms ID/version, exact selection, `mode=SPECIFIC_SLOT`, paymentMethod and a stable UUID Idempotency-Key. Preserve that key through retries of the same intent.
4. Poll the returned receipt. Once it has a reservation ID, read `/api/me/reservations/{id}`. When an actual orderId exists, invoke the Payment owner's protected checkout for that order. Display the exact returned H; the browser must not extend it. Genuine zero-price terms use no Payment order.
5. After the payment redirect/interaction, GET the same receipt/booking again. Continue pending display while owner verification and durable commit are unresolved. Closing the browser must not stop the backend. Show success/redirect to confirmed details only when the backend state is confirmed; provider success alone is insufficient.

Example: Driver selects A3 for 11:00–15:00. Preview shows 4,000 VND; after a 50% prepayment method/terms selection, review shows 2,000 VND due now. These are example owner terms, not a universal deposit default. Submission at 10:30 with an effective five-minute hold shows H=10:35. Payment verified while the hold is live may progress; a redirect at 10:34 does not itself confirm. If confirmation evaluation time is 10:59:59 and durable commit completes at 11:00:02, the approved checkpoint rule can qualify. A debit before 11:00 first verified after 11:00 cannot create a new qualifying confirmation.

Example: the user refreshes after submitting. Resume the same receipt/key; do not create another booking/order. If the form is edited after review, get new terms and a new intent key. If payment is UNKNOWN, display pending reconciliation rather than offering a fresh charge. Actual Payment QR/bank fields must come from the provider response, never local placeholders.

## Next-phase scan, reallocation and checkout handoff

The user explicitly excludes these from the current through-confirmation deliverable. Preserve the [approved rules and remaining questions](../decision-log/2026-11-09-reservation-confirmation-and-arrival.md):

- Allow walk-in despite the plate's future reservation. Correlate by canonical plate and authoritative check-in/scan/assignment evidence.
- Early entry before T on the intended slot adds early-time charges. If observed elsewhere, recover/reallocate and release unused original protection once, while preserving actual occupancy and recovering affected future claimants within allocation lead.
- Four booked hours at 1,000 VND/hour, 2,000 already collected, and one early + one booked hour used means **3,000 VND** due at checkout. The full booked price remains due; credit money once, no refund solely for unused booked time.
- Wrong parking adds a separate fee. Admin defines default/bounds, Owner may override per lot. Unusable assigned slot or Operator/system-authorized relocation waives it. Exact-T arrival is on time; enabled late tolerance has the existing minimum 15 minutes. Amount/formula and complete evidence predicates remain for next-phase design; do not invent UI or backend defaults.
- Show requested slot, assigned slot and observed slot distinctly. Audit history is produced through durable events and consumed by its owning service.

## 11 November 2026 — amended creation, payment and confirmation flow

**Status:** approved implementation direction; not a claim of FE/backend readiness. This section supersedes older wording that keeps the create workflow open while awaiting payment. See the [authoritative 11 November decision](../decision-log/2026-11-11-reservation-event-driven-confirmation.md) and [cross-team checklist](2026-11-11-SPARK-273-implementation-alignment.md).

The Driver experience remains: **choose Specific Slot → choose registered CAR, interval and payment method → review authoritative versioned pricing + operational terms → submit one reservation intent → wait for committed `PENDING_PAYMENT` hold → wait for actual Payment order → pay via existing payOS checkout → observe the same Operations booking until `CONFIRMED`**.

- The successful **request workflow** terminates at durable hold commit (proposed terminal receipt token `HOLD_CREATED`, pending final API agreement). The reservation itself remains `PENDING_PAYMENT` until the separate Operations event consumer confirms it. A booking lifecycle `COMPLETED` must not be shown as the result of paying a deposit.
- The frontend must not infer a hold from 202/authorization success alone or from a selected visual slot. Upon request receipt success, read the associated `reservationId`, `holdExpiresAt=H` and later nullable `orderId` from the own-booking endpoint. Order provisioning happens *after* hold commit, so the order can briefly be unavailable. Display "Preparing secure payment" rather than a fabricated QR or fake order.
- Preserve `Idempotency-Key` on replay/reload and the accepted `termsId`/`termsVersion`, financial quote/coverage version and displayed amounts. Changed/expired terms require explicit renewed review; backend cannot silently price at a newer tariff without Driver acceptance. Amount and deposit mode come from Payment-owned authoritative pricing and configuration, not `driverData`/local hourly arithmetic.
- With a real `orderId`, the **authenticated Driver** calls Payment's existing `POST /api/v1/payments/orders/{orderId}/checkout`; Operations does not have to proxy. Payment's server expiry uses exact H (ordinary five-minute policy default, capped at T), not a frontend default. At/after H, disable **new** checkout attempts. A delayed bank settlement may still be discovered and reconciled; do not claim the payment was impossible just because H passed.
- After payment redirect or QR flow, read `GET /api/me/reservations/{reservationId}` and optionally receipt/payment state; **do not issue a new confirmation command** or create a new order. Display distinct states for Payment verified, Operations `CONFIRMED` and refund/reconciliation. A provider-success page is not entitlement, and a collection arriving after H may need pre-T capacity reacquisition or refund.
- SignalR is only a scoped notification prompting an authorized backend refresh. Resume after refresh/account switch cautiously; prevent foreign Account details and do not automatically restart `UNKNOWN` checkout attempts.

**Frontend release examples:** double-click reserve does not create two holds; refresh during order provisioning resumes same id; checkout on other Account is rejected; no order at H is handled without invented QR; provider marks paid while booking remains pending/expired; Facility invalidation prompts refresh; accepted tariff changes require renewed review. No UI claim of allocation, Gate entry or completed parking follows reservation confirmation alone.
