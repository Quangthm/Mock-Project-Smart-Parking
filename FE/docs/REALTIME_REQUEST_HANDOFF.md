# Frontend request reduction and event-contract handoff

Scope: frontend inspection and implementation only. Backend source and runtime were not inspected for this task. Event capabilities below are based only on existing frontend subscriptions; they are not proof that every backend deployment publishes them.

## Implemented frontend behavior

- Concurrent identical GETs share one in-flight request per login and query. Results are not cached after completion; mutations invalidate pending-read sharing.
- Admin lists, Owner capacity/structure/lists, Driver reservation lists and Operator dashboard refresh on initial load, existing mutations/manual refresh and browser resume instead of periodic background polling.
- Existing Operator layout events (`SlotStateChanged`, `OccupancyChanged`, `OccupancyReleased`, `SnapshotRequired`) are grouped for 300 ms. Only one snapshot read runs at a time, with one trailing invalidation retained if changes arrive during that read.
- Layout events refresh the selected site's layout rather than all assigned sites. Occupancy events also invalidate reservation data. They do not reload tickets or notifications. Hidden-tab invalidations wait until resume.
- Operator resources no longer refetch twice through both `refreshKey` and `sitesRevision`.
- Invalid non-UUID site IDs do not create layout connections. Layout uses WebSockets only, with no silent SSE/Long Polling fallback. A subscription makes at most three automatic connection attempts with increasing delays and jitter; resume triggers cannot bypass cooldown/budget, and recognized permanent failures (including nested errors) stop retries. Replacement subscriptions wait for connection shutdown. A visible unavailable state and explicit retry keep a failed connection from being presented as live data.
- Owner `OperatorsChanged` refreshes the Operator roster rather than the parking-site list as well.

## Backend handoff required (PROPOSED, not implemented events)

For each resource below, the backend team must confirm an existing contract or agree a new one before frontend subscription work. Do not assume event names or payloads.

| Resource | Required committed-change notifications | Proposed owning service to confirm |
| --- | --- | --- |
| Owner site capacity/structure/list | Site created/edited/activated; structure or capacity changed | Facility / Operations |
| Admin users/applications | User state/profile changed; application submitted/reviewed | UserService |
| Driver reservations | Reservation state, allocation or payment outcome changed | Operations |
| Operator assignments | Assignment created/removed/changed | UserService |
| Operator tickets/notifications | Ticket created/resolved; notification created/read | Relevant resource-owning service |
| Payment/reservation request progress | Verified payment outcome and request state changed | Payment / Operations |

Each contract must define authorized account/tenant/site recipients; resource IDs and event identity/version; publish-after-commit behavior; authoritative snapshot endpoint; reconnect reconciliation; duplicate/out-of-order handling and revocation behavior. Missing events mean remote changes may appear only after manual refresh, browser resume or reopening a screen. FE does not claim full realtime coverage for these resources.

## Existing polling retained, not expanded

Payment QR status and in-progress reservation checkout retain their existing 5-second polling to avoid breaking financial confirmation without a verified push contract. The payment return page retains its existing maximum of 12 checks. Live camera recognition retains its existing sequential capture/request loop with a 750 ms wait, and pending demo slot registration retains its existing five-minute scan. These are not newly approved polling patterns. Any replacement, expansion or new fallback must follow the project-owner approval gate in the shared coding skill.

## Verification limits

Automated tests exercise request sharing, failure cleanup, event bursts, trailing snapshots, visibility and subscription cleanup. Browser RPS and production capacity require separate measurement. A Vite dev-server module count is not a backend RPS measurement. No backend/configuration change is included.
