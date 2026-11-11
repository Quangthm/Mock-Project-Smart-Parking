# SmartParking v1.1 frontend realtime audit

Phase 1. Static review of the local checkout on `fe-v2.3`. Hub, authorization and API findings are source observations. Browser connectivity, deployed configuration and cross-session synchronization have not been verified.

## Verified SignalR contract

| Item | Source finding |
| --- | --- |
| Hub | UserService maps `/hubs/operators` with `CloseOnAuthenticationExpiration = true` in `SmartParking.UserService.API/Program.cs`. No other SignalR hub mapping was found in the checked-out backend. |
| Event | `OperatorsChanged` is a payload-free invalidation hint. `OperatorAccessHub.cs` sends it to individual verified Owner connections whose current tenant scope intersects the event's tenant IDs. |
| Server reconciliation | `Reload()` rechecks session validity and runs the scoped operator list query. It returns an operator list. The existing HTTP equivalent is Owner-scoped `GET /api/operators`. |
| Authentication | The hub requires the Owner role, an active JWT session and at least one tenant granting `OPERATOR_MANAGE`. `CurrentAccountBearerEvents` accepts the SignalR query token for this hub path and validates current access. No client-invoked site group join was found. |
| Event emission | `CreateOperator` and `ActivateOperator` publish `OperatorAccessChanged`. The configured messaging consumer invokes the notifier. Runtime delivery depends on deployment configuration. No equivalent emission was verified for every assignment or permission edit. |
| Access revocation | FE subscribes to `AccessRevoked` on `/hubs/access` and clears session/context on receipt. The checked-out BE contains no mapping or emission for that contract. The Owner hub can abort stale connections. Protected HTTP access checks remain independent of SignalR. |
| Direct database writes | No PostgreSQL notification or change stream wired to the hub was found. Authorized API reads can eventually reflect direct writes when their server read model and caches expose the committed change. |

## Compatibility matrix

| Feature | Existing hub | Existing event | Refetch API | FE ready today | BE dependency |
| --- | --- | --- | --- | --- | --- |
| Parking availability | None verified | None verified | Facility structure and operational layout for authorized management roles | Operator has layout refresh; Owner can refetch; Driver map uses local store | Driver-authorized discovery and vehicle-specific availability reads still need a verified contract |
| Booking status | None verified | None verified | Operation `GET /api/reservations?siteId=…` for authorized site workflows | Operator reads reservations; Driver booking/history use local sources | Driver-scoped authoritative current/history and cross-session booking flow need verification |
| Check-in / check-out | None verified | None verified | Operation parking sessions and reservations, Facility operational layout/structure | Operator workflow and refresh exist | Push emission and live Owner/Operator reconciliation still need verification |
| Parking prices and deposit | None verified | None verified | Payment calculation exists. No verified management/read API for the requested published pricing and deposit settings | Local lot metadata and Owner drafts exist | Authoritative published settings and Driver-visible read contract |
| Parking policies | None verified | None verified | No verified API for published Owner policy | Owner drafts and legacy Driver documents use browser data; Platform content is separate | Effective policy API and publishing/versioning contract |
| Payment status and history | None verified | None verified | Payment transaction status, invoice lookup and Driver-scoped invoice history exist in BE | PayOS checkout polls transaction status. Driver invoice history integration still needs wiring/contract verification | Push event and linkage between invoice/transaction data and individual booking screens |
| Owner approval | None verified | None verified | Owner applications list/detail/review used by Admin | Admin can refetch. No Owner-facing application-status read has been verified | Owner-scoped approval status and push event |
| Operator assignment | `/hubs/operators`, Owner only | `OperatorsChanged`, payload-free, verified for creation/activation pipeline | Owner operator list; Operator `/api/operators/me` and authorized site/layout reads | Owner screen listens; Operator dashboard reads assigned sites | Push coverage for assignment edits and delivery to the Operator are unverified |
| Access revocation | FE expects `/hubs/access`; absent in checked-out BE | FE expects `AccessRevoked`; absent in checked-out BE | Protected requests and auth expiry handling | FE lifecycle and mocked revocation handlers exist | Server hub/event contract required for immediate push revocation |
| Admin users and overview | None verified | None verified | Admin user and application reads | Users already poll every 20 seconds and refresh on visibility; other screens use their own fetch effects | Push events and authoritative overview metrics where current widgets use local data |
| Passes and notifications | None verified | None verified | Existing Operator site-scoped notifications; no verified Driver passes read | Driver Passes uses browser data; notification sources differ by role | Authoritative APIs for resources still served only by browser data |

## Current frontend architecture

- `FE/src/lib/operatorLive.ts` owns a SignalR connection per `watchOperators` call. Owner Operator Management is its current consumer. It refetches on connection and event, listens to auth/visibility/focus changes and retries with token-expiry checks.
- `FE/src/lib/accessRealtime.ts` is mounted by `AppContext`. It obtains the current token through `ensureAccessToken`, retries optional transport failures and handles explicit mocked revocation. A transport error alone does not log the user out.
- Authenticated requests flow through `authApi` or existing service adapters. Owner and Operator use scoped Facility/Operation clients. Driver map and Passes, and parts of financial/history UI, use the browser store. Local `sp-data-change` and `sp-operator-refresh` events do not synchronize independent browser sessions.
- `useLotStatus` already refetches operational layout on visibility/focus and handles stale data. PayOS checkout polls status every five seconds. Admin user management already polls and responds to visibility. Some other data loads are mount-, site-, or manual-refresh-driven.

## Proposed Phase 2

1. Extend the existing Owner hub watcher and share its lifecycle within the authenticated role scope. Treat verified hints as invalidations and coalesce bursts. Reconcile the Owner operator list on connection and reconnect via an existing authorized read. Surface connection status on the consuming screen when useful.
2. Reuse existing screen loaders for authorized REST reads. Add scoped foreground/focus/online reconciliation and configurable visible-tab polling for selected high-value resources without hub events. Avoid a second polling loop for resources already polled. Preserve live screen state, including maps, pagination, searches, dialogs and drafts.
3. Protect each resource refresh by authentication scope, site and request generation. On role/site change or logout, stop old subscriptions and discard late responses. Confirm loss of assignment from the backend and clear data for a site when authorization is lost. Keep existing HTTP-based session enforcement and distinguish transport failures from confirmed revocation.
4. Integrate only verified role/API combinations. Leave resources without an authoritative endpoint marked **Blocked by Backend**. Evaluate the existing Driver invoice history route separately from PayOS status and keep current checkout behavior.
5. Run TypeScript, build and focused mocked tests for burst coalescing, reconnect, cleanup and role/site isolation. Use a deployed test environment for browser cross-session cases and record unsupported cases separately. A direct database modification requires an approved test fixture and operator and was not performed in this audit.

## Regression risks to cover

- React StrictMode duplicate listeners, session refresh during reconnect and out-of-order responses after site/role changes.
- Background refresh replacing the active booking form, selected slot, map camera or editing state. A confirmed slot conflict must block confirmation and ask the Driver to reselect.
- Inaccurate Driver data if an Owner-only API is treated as public discovery, or if local prices are presented as authoritative.
- Request amplification across tabs, duplicate polling on the 3D view, and transient reads interpreted as lost authorization.
- Admin/Owner approval data exposure if a management route is reused without verifying the Owner-specific scope.
