# Reservation first slice — frontend handoff and readiness

Reviewed 9 November 2026 in the local SPARK-273 checkout. Source inspection only: no frontend changes, frontend build, application startup, live API test or Docker publish verification was performed. Backend compilation after the refactor remains unverified. This is a backlog for frontend/backend teammates, not a claim that bookings work.

## What can be tested now

After the current backend passes build/tests and is deployed with aligned schemas, credentials and messaging, Postman or another HTTP client can exercise **admission and receipt**, not a valid confirmed booking:

1. Sign in as a real active Driver and obtain its JWT.
2. POST `/api/reservation-requests` with a UUID `Idempotency-Key` and the documented intent body.
3. GET the returned receipt Location using the same Account.
4. Observe pending authorization, denial, timeout or the deliberate `FAILED_DEPENDENCY / RESERVATION_FINALIZATION_NOT_READY` result after Allowed. There must be no booking, capacity claim, payment order or refund effect.

Required IDs and terms fields currently undergo shape checks; admission does not prove vehicle ownership, site/slot validity, quote issuance or commercial acceptance. Fabricated terms IDs may exercise the gated admission contract in an isolated diagnostic test, but must never be presented as a valid reviewed booking. No live terms endpoint exists yet.

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
| P0 / Operations + Facility + User | Wire authoritative verified own-CAR binding, actual site/slot eligibility, database policies, concurrency-safe vehicle/resource/quota acceptance | Real site/slot/vehicle facts are validated, including same stable vehicle across lots and live holds. Local browser state never decides capacity. |
| P0 / Payment + coverage owners | Implement the published versioned quote and coordinated coverage producer contracts | Review/acceptance uses real versions and financial terms; no missing-price-to-zero fallback. |
| P0 / Operations + architecture owner | Complete reviewed strict commit-before-start evidence and finalization path | D4c is demonstrated; zero-price bookings also stay gated until then. Do not quietly change cutoff semantics. |
| P0 / Operations | Complete booking lifecycle, allocation/recovery, F/T handling, refund intents and cancellation orchestration needed by the first slice | Rules are integrated into durable workflows; pure Domain tests alone are insufficient. |
| P0 / API owners + frontend | Finalize own-booking list/detail/cancellation routes and response fields | Proposed Driver `/api/reservations` list conflicts with the existing Operator route; resolve before implementing a client. Do not treat proposed routes as deployed. |
| P1 / frontend | Add an Operations API adapter for request admission and own receipt reads using existing auth conventions and configured base URL | Stable per-intent idempotency key, same-key retry after ambiguous network outcome, receipt resumption after refresh, Account-scoped isolation. No bearer/key secrets persisted in ordinary UI state. |
| P1 / frontend | Separate request progress from booking lifecycle | 202 means request received, not reserved. Display pending/denied/unavailable honestly. `RESERVATION_FINALIZATION_NOT_READY` shows service unavailable, not confirmed or paid. No automatic creation loop after terminal failure. |
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

Before implementing dependent behavior, resolve: the exact post-payment endpoint semantics and provider verification/reconciliation; cash/deferred payment and zero-price/coverage paths; authoritative terms disclosure if returned terms differ from what was shown; allocation/hold policy when inputs are not complete; and D4c evidence. Do not silently choose these while preparing this commit. Keep full booking implementation paused until the user resumes after committing.
