# Frontend request reduction and event-contract handoff

> Historical scope note: the original handoff inspected frontend source only and did not claim backend runtime proof. Checkpoint E additionally inspected committed backend contracts; the current status below supersedes that limitation without claiming deployed runtime acceptance.

## Current SPARK-273 status (2026-11-26)

- There is **no recurring Driver reservation-progression polling** and **no recurring reservation checkout-readiness polling**. `../src/lib/reservationProgress.ts` subscribes to `/hubs/reservation-progress`, accepts `ReservationProgress` and `ReservationCheckoutChanged`, and performs one authoritative progress/checkout read on initial join, reconnect, or explicit page restoration. No timer drives those reads.
- Payment authority remains separate from Operations booking state. Operations `OrderId` is nullable; Payment creates the payable order independently and is authoritative for checkout readiness. Do not use Operations `OrderId` or `HoldExpiresAt` as checkout evidence.
- Independent Payment QR/transaction status checks remain where genuinely standalone or manually entered: `../src/roles/driver/payments/PaymentCheckout.tsx` uses `../src/lib/serverRefresh.ts` with a 5-second interval only when no reservation/order was supplied, and `../src/roles/driver/payments/PaymentResultPage.tsx` bounds retries to 11 for standalone workflows only. These Payment-scoped checks are **not** authorization for reservation lifecycle polling.
- Current contract references: [reservation API](../../docs/03-design/api/reservation-first-slice-api-design.md), [workflow target](../../docs/02-architecture/architecture-decisions/reservation-workflow-target.md), [manual test guide](../../docs/04-testing/postman/SPARK-273-manual-test-guide.md), [deployment checklist](../../docs/04-testing/reports/SPARK-273-deployment-checklist.md#checkpoint-d-coordinated-cutover-and-recovery), and [Checkpoint E tester handoff](../../docs/04-testing/reports/SPARK-273-checkpoint-e-tester-handoff.md).
- Checkpoint E made **no frontend production-code changes**; this document is a handoff/correction only.

## Implemented frontend behavior

- Concurrent identical GETs share one in-flight request per login and query. Results are not cached after completion; mutations invalidate pending-read sharing.
- Admin lists, Owner capacity/structure/lists, Driver reservation lists and Operator dashboard refresh on initial load, existing mutations/manual refresh and browser resume instead of periodic background polling.
- Existing Operator layout events (`SlotStateChanged`, `OccupancyChanged`, `OccupancyReleased`, `SnapshotRequired`) are grouped for 300 ms. Only one snapshot read runs at a time, with one trailing invalidation retained if changes arrive during that read.
- Layout events refresh the selected site's layout rather than all assigned sites. Occupancy events also invalidate reservation data. They do not reload tickets or notifications. Hidden-tab invalidations wait until resume.
- Operator resources no longer refetch twice through both `refreshKey` and `sitesRevision`.
- Invalid non-UUID site IDs do not create layout connections. Layout uses WebSockets only, with no silent SSE/Long Polling fallback. A subscription makes at most three automatic connection attempts with increasing delays and jitter; resume triggers cannot bypass cooldown/budget, and recognized permanent failures (including nested errors) stop retries. Replacement subscriptions wait for connection shutdown. A visible unavailable state and explicit retry keep a failed connection from being presented as live data.
- Owner `OperatorsChanged` refreshes the Operator roster rather than the parking-site list as well.

## Backend handoff required (current contract status)

For each resource below, the backend team must confirm an existing contract or agree a new one before frontend subscription work. Do not assume event names or payloads. The reservation progress row is now backed by the committed Operations hub contract; the other rows remain proposals unless separately implemented.

| Resource | Required committed-change notifications | Proposed owning service to confirm |
| --- | --- | --- |
| Owner site capacity/structure/list | Site created/edited/activated; structure or capacity changed | Facility / Operations |
| Admin users/applications | User state/profile changed; application submitted/reviewed | UserService |
| Driver reservations | Reservation state, allocation or payment outcome changed | Operations |
| Operator assignments | Assignment created/removed/changed | UserService |
| Operator tickets/notifications | Ticket created/resolved; notification created/read | Relevant resource-owning service |
| Payment/reservation request progress | Implemented Operations events: `ReservationProgress` and `ReservationCheckoutChanged` on `/hubs/reservation-progress`, with one authoritative recovery read on join/reconnect/page restore | Operations |

Each contract must define authorized account/tenant/site recipients; resource IDs and event identity/version; publish-after-commit behavior; authoritative snapshot endpoint; reconnect reconciliation; duplicate/out-of-order handling and revocation behavior. Missing events mean remote changes may appear only after manual refresh, browser resume or reopening a screen. FE does not claim full realtime coverage for these resources.

## Existing polling retained, not expanded

- Reservation lifecycle: no recurring progression polling and no recurring checkout-readiness polling. Use the SignalR events and one authoritative read on initial join, reconnect, or explicit page restoration; do not add a timer-driven fallback.
- Standalone Payment QR/transaction status: the existing 5-second `useServerRefresh` interval remains for manual/standalone checkout only (`!providedOrderId` in `PaymentCheckout.tsx`). It is not reservation checkout polling.
- Payment return: the existing maximum of 11 standalone retries remains only when there is no reservation hint and the order association is standalone; reservation-bound returns rely on the Payment lookup plus the reservation progress subscription, not recurring checkout polling.
- Live camera recognition retains its existing sequential capture/request loop with a 750 ms wait, and pending demo slot registration retains its existing five-minute scan.
- These are not newly approved polling patterns. Any replacement, expansion or new fallback must follow the project-owner approval gate in the shared coding skill.

## Verification limits

Automated tests exercise request sharing, failure cleanup, event bursts, trailing snapshots, visibility and subscription cleanup. Browser RPS and production capacity require separate measurement. A Vite dev-server module count is not a backend RPS measurement. No backend/configuration change is included.
