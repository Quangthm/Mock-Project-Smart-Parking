# Frontend realtime synchronization

The [audit](./SIGNALR-FE-AUDIT.md) records the contracts found in this checkout. Only UserService's Owner-scoped `OperatorsChanged` is wired as a verified SignalR hint. It carries no resource data.

## Synchronization paths

| Screen | Trigger | Authoritative read |
| --- | --- | --- |
| Owner → Operators | `OperatorsChanged` and successful connect/reconnect | Existing Owner operator list and its site options |
| Owner → Dashboard, My Parking Lots, Parking Structure | Foreground reconciliation, 30 seconds for dashboard capacity and selected structure, 45 seconds for lot comparison | Facility structure and authorized lot list where the existing screen loader uses it |
| Operator dashboard, assignments and map | Foreground reconciliation at 45 seconds, site/role changes and existing action refresh events | Operator assignments and Facility layouts, followed by existing authorized site reservations, tickets, notifications or financial report reads |
| Admin → User Management | Foreground reconciliation at 20 seconds | Current user query and open user detail |
| Admin → Applications | Foreground reconciliation at 30 seconds | Existing authorized application list |
| Driver → PayOS checkout | Foreground reconciliation at 5 seconds, while the transaction is pending | Payment transaction status, with order identity and authoritative success checks |

The Operator checks assignments before applying a fresh site snapshot and resets the map when its authorized site changes or disappears. The Backup UI derives its permission from the current assignment snapshot. The backend remains responsible for authorizing every read and mutation. Manual refresh and the existing check-in and checkout action callbacks remain available. Automatic dashboard polling is paused on Gate Check-In and Gate Check-Out to preserve active forms.

## Lifecycle and performance

- `operatorLive.ts` shares a single Owner hub connection per JS runtime while consumers are mounted. It uses the existing token factory, expiry event, retry and resume paths. Final unsubscribe removes listeners and waits for the previous stop before starting a replacement. A 250 ms window coalesces repeated event hints. Connect and reconnect trigger authoritative reconciliation because hints are not durable.
- `serverRefresh.ts` reuses existing loaders. Intervals are configured by each consumer. It also listens to focus, visibility and online changes, skips hidden/offline tabs and avoids refreshing an unattended session near token expiry. Asynchronous subscribers can check whether their scope is still current. Screen loaders use effect cancellation flags or pending guards to discard stale results and avoid redundant requests.
- Background refresh retains existing results where practical. Initial loading remains separate on the updated lot, structure and application screens. Owner structure pauses its polling while registering slots or editing compatibility. Admin applications defer reads while a review is in progress.
- The existing FE `AccessRevoked` handler remains in place for compatible deployments. This checkout does not contain the corresponding server hub or event. A failed connection is not evidence of revocation.

## Boundaries

Direct database changes do not generate verified SignalR events. Foreground polling is eventual and only reflects changes exposed by the authorized API and its backing read model. The configured interval is not an end-to-end latency guarantee.

Driver map discovery, booking and slot conflict enforcement, Owner policies, pricing and deposits, Driver passes, and Owner-facing approval status were not wired to unverified APIs or events. The Payment Service's separate Driver invoice-history route was identified but is not integrated into the current local booking history by this change. Admin overview aggregates and cross-session notification delivery were not verified. See the audit for missing contracts.

## Files

- Shared: `src/lib/serverRefresh.ts`, `src/lib/operatorLive.ts`, `src/lib/operatorApi.ts`
- Owner: `src/roles/owner/dashboard/OwnerOverview.tsx`, `src/roles/owner/parking-lots/useLotManagement.ts`, `src/roles/owner/parking-lots/LotManagement.tsx`, `src/roles/owner/structure/ParkingStructure.tsx`
- Operator: `src/roles/operator/dashboard/OperatorDashboard.tsx`, `src/roles/operator/parking-lot/LotStatus.tsx`, `src/roles/operator/parking-lot/useLotStatus.ts`, `src/roles/operator/parking-slots/SlotManagement.tsx`
- Admin: `src/roles/admin/users/useUsers.ts`, `src/roles/admin/applications/Applications.tsx`
- Driver: `src/roles/driver/payments/PaymentCheckout.tsx`
- Tests: `tests/server-realtime.test.cjs` (relative to FE)
