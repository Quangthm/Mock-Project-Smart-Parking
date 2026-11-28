# SmartParking — MVP frontend integration handoff

**Date:** 28 November 2026  
**Remote baseline inspected:** `develop` at [`c1343a489`](https://github.com/Quangthm/Mock-Project-Smart-Parking/commit/c1343a489ae603ac9d886d8096c4027e1b20c800)  
**Goal:** Replace misleading browser-local business data with existing authorized backend reads and writes before the MVP code freeze. Preserve current layout and working flows. This is a proposed work handoff, not approval to redesign contracts or proof of runtime acceptance.

## 1. Read first and implementation boundaries

Read `AGENTS.md`, `ai/AGENTS.md`, `ai/rule.md`, `docs/07-skills/SmartPark-Coding-Skill-v1.0.md`, `FE/AGENTS.md`, `FE/.agent/lead-frontend.md` and relevant current API contracts. Inspect current branch and worktree before changes. Present a minimal source-backed plan for approval. Work in independent slices; preserve teammate changes; do not stage, commit or push automatically. Reuse the shared authenticated request client and actual service configuration; inspect DTOs, errors and account/session identity before mapping. Do not introduce polling for real-time features; use existing SignalR contracts if available, an authorized read on initial load/reconnect, and explicit refresh as a fallback. Report missing event producers rather than inventing hubs or events.

No localStorage/fixture fallback for real business outcomes. Local UI preferences are fine. Existing explicitly labelled isolated demo fixtures may remain separate. Loading, empty, unavailable, forbidden and stale are distinct states. Clear cached business state on logout, account/role switch and relevant site changes; discard late responses from previous sessions. Never show a success toast or change authoritative state before confirmed server outcome. Do not bypass authorization or call another role's endpoint with fabricated identity.

## 2. Work order and backend dependencies

| Slice | Priority | Estimated FE effort | Readiness |
|---|---|---:|---|
| A. Notifications | P1 | 3–5h | Existing API; recipient/Account isolation and event coverage require confirmation |
| B. Admin audit | P1 | 3–5h | Existing Admin API; integrate now |
| C. Owner revenue | P1 | 4–6h FE after contract | Only pass-revenue summary verified; general settlement report requires Payment handoff (overall previous estimate 6–10h, not guaranteed) |
| D. Driver support/appeals | P2 | 3–5h FE after contract | Blocked on authorized Driver case API; SPARK-335 / 353 / 354 / 355 |
| E. Admin platform settings | P2 | 3–6h FE after contract | Persistent platform-policy contract not established; SPARK-340 |

Estimates are provisional person-hours, not commitments. Complete A and B first; parallelize C–E contract checks. Do not wait on a blocked slice to implement independent work. No scope expansion into full refunds, account recovery, or new platform policy management.

## 3. Slice A — Notification bell

**SRS:** FR-RPT-01, FR-BAS-06, UC-NOT-01, US-D11; Jira SPARK-287 / SPARK-308.

**Source:** `FE/src/components/navigation/NotificationBell.tsx` currently reads `store.getNotifications(userId)`, `markNotificationRead`, `markAllNotificationsRead` from the browser store. `FE/src/lib/store.ts` stores these in localStorage. Backend `NotificationsController.cs` exposes:

- `GET /api/v1/notifications?status=...&limit=50` (status enum and DTO must be checked; status must not be confused with read flag).
- `GET /api/v1/notifications/{id}`
- `PATCH /api/v1/notifications/{id}/read`
- `POST /api/v1/notifications/mark-all-read`

Alias `/api/notifications` exists. Responses wrap `success` and `data`. List recipient is derived from authenticated UserId in current controller, not supplied by browser. **Security dependency:** confirm whether User-level notification scope is intended for every category; do not expose Account/role-specific notifications across accounts. Controller name alone does not establish full ownership isolation; verify handlers. If backend cannot distinguish required scopes or does not publish realtime notifications, report exact gap to Notification teammate. Do not implement client-side authorization as substitute. Do not use staff delivery-retry endpoint from the bell.

Keep existing notification appearance. Add/reuse typed API client, initial and reconnect reads, read/mark-all operations, error handling and accurate unread count for loaded results (do not imply global count unless server supplies it). Use an existing supported realtime subscription only after verifying contract. Show supported business-event notifications; do not fabricate missing categories or imply email/SMS delivery from in-app data.

**Acceptance:** real reservation/payment notification appears if producer is wired; read and mark-all persist after reload; other recipient's ID is rejected; user/account change clears previous results; API failure is visible without mock fallback; missing event support documented.

## 4. Slice B — Admin audit

**SRS:** FR-RPT-03/04/05, UC-RPT-02, US-A03; Jira SPARK-338 / SPARK-309.

**Source:** `FE/src/roles/admin/audit-log/AuditLogView.tsx` imports `adminData` and reads local `getAuditLogs()`.

Backend `AuditProjectionsController.cs` is `AdminOnly` and exposes:

- `GET /api/v1/audit-projections` with optional `actorId`, `actorRole`, `action`, `originatingService`, `resourceType`, `resourceId`, `lotId`, `search`, `from`, `to`, `page` (1), `pageSize` (20).
- `GET /api/v1/audit-projections/{id}`

Alias `/api/audit-projections` exists. Map real page and detail DTOs. Render safe returned actor/action/resource/time/source data, support server-side filters and pagination, and show immutable audit detail only; no editing or deletion. Never generate audit evidence in FE. Missing source events must be reported to originating service; absence is not proof that no action occurred. Do not request tokens, secrets, or sensitive raw payloads.

**Acceptance:** Admin sees a real projected audit event; filter by time/actor/action/lot, paginate, open detail; non-Admin is denied; no local fixtures on API failure. Coordinate with SPARK-237 for actual administrative access-change events.

## 5. Slice C — Owner revenue

**SRS:** FR-RPT-02, FR-BAS-07, UC-RPT-01, US-OW04.

**Source:** `FE/src/roles/owner/revenue/RevenueDashboard.tsx` calculates amounts from `store.getBookingsByLot`, uses local pass data, and labels `Math.round(revenue * 0.85)` as profit/net settlement. Refunds are marked not tracked. These are not authoritative financial results.

Verified backend endpoint: `GET /api/v1/payments/owners/lots/{siteId}/pass-revenue-summary?from=...&to=...&productType=...` (`PassRevenueController.cs`, OwnerLotManagementPolicy). This is **pass revenue only**, not general parking revenue. Do not rename it to total revenue or extrapolate. Coordinate with Payment for an existing scoped settled parking-revenue/transaction report or minimal approved API contract. The Payment service must supply collected/settled amounts and verified refund status; Operations supplies visit facts as needed through authorized contracts. Do not use Driver invoices as an Owner report or query another service's DB.

Remove the arbitrary 85% assumption, fake profit/net settlement, fake method distribution and fabricated refund totals. Present only available authoritative metrics, clearly labelled by category and date/site scope. If general revenue API is absent, display genuine supported pass summary in a separately labelled section, and mark other metrics unavailable until backend ready. Day/week/month/quarter, registered/walk-in segmentation are SRS targets; do not claim completion for missing fields. Date boundaries/time zone and currency must follow backend contract; do not sum incompatible amounts.

**Acceptance:** totals match known settled records and verified refund status; date/site filters affect data; unowned site denied; pending/unknown payments not counted as settled; no fake profit; unavailable data distinguished from zero. If backend blocked, record the exact missing endpoint/data contract and stop that slice rather than inventing frontend metrics.

## 6. Slice D — Driver support and parking appeals

**SRS:** FR-INC-05/07, FR-PAY-06, UC-INC-02; SPARK-335 and subtasks SPARK-353/354/355. Keep separate from UC-AUTH-06 account recovery.

**Source:** `FE/src/roles/driver/support/DriverSupport.tsx` reads local bookings/tickets and calls `store.createTicket()` and `store.createNotification()`. Facility `OperatorOperationsController.cs` offers assigned-Operator-scoped `/api/operator/sites/{siteId}/tickets` and `/incidents`, not a Driver case submission contract. Do not repurpose those routes for Driver access or widen their guards.

Coordinate with backend owner for authenticated own eligible visit/charge/incident references, reason and permitted evidence, idempotent submission, own-case list/detail and customer-safe status. Use real eligible records for selection. Operator review state (submitted/in review/accepted/rejected) is distinct from Payment payout state (pending/unknown/completed/failed); never present ACCEPTED as REFUNDED. Owner escalation only via authorized workflow. Redact third-party/staff data; no secrets or unapproved evidence types. Do not create local tickets or notifications when backend fails. If case API is not ready, mark production submission unavailable; preserve UI only as explicitly labelled demo, if desired and approved.

**Acceptance:** Driver creates one persisted own case, sees it after reload, duplicate retry is safe, unauthorized case/visit access denied, Operator decision appears via authorized projection, refund status is separate and authoritative. This slice does not implement refund calculation, payout or credential recovery in FE.

## 7. Slice E — Admin platform settings

**SRS:** UC-POL-01/02; SPARK-340. `FE/src/roles/admin/system/SystemConfig.tsx` holds local React configuration and a saved flag. Inspect the current Admin settings and AI settings integrations before deciding what can be reused.

Only bind controls backed by approved persistent platform policy/integration APIs with actual Admin authorization, bounds and validation. Confirm read/write contracts, effective vs draft values and concurrency behavior with service owner. Do not add role management, expose provider secrets, or invent configurable defaults. Mark unsupported controls unavailable/read-only, remove fake save success for real mode. Do not alter existing working Assistant settings or Owner policies.

**Acceptance:** authorized changes persist after reload; rejected/out-of-bounds values do not alter displayed committed configuration; unauthorized requests denied; secrets never returned/displayed; absent backend contracts reported. Full platform setting implementation is not a frontend-only task.

## 8. Testing and delivery

- Focused existing frontend tests for DTO mapping, authorized request paths, scope changes, pending/error/empty state, mutations and stale responses; no elaborate frontend simulations.
- Run necessary scoped TypeScript checks and frontend production build once after integrated changes; disclose unrelated baseline diagnostics.
- Provide a short manual UAT handoff: role, fixture prerequisites, route, action, expected persisted outcome, negative test. User performs manual browser acceptance. Do not claim live integration from build alone.
- Report each slice as API-integrated, blocked on backend, or verified in runtime (with evidence). Include changed files, actual endpoint/DTO, missing producers/contracts, tests, risks and estimated remaining work. Stop before unrelated features. No automatic stage/commit/push.

## References

- [SRS notifications/reporting](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/c1343a489ae603ac9d886d8096c4027e1b20c800/docs/01-requirements/srs/modules/08-notifications-reporting.md)
- [Appeal use case](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/c1343a489ae603ac9d886d8096c4027e1b20c800/docs/01-requirements/use-cases/uc-inc-02.md)
- [Notification bell](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/c1343a489ae603ac9d886d8096c4027e1b20c800/FE/src/components/navigation/NotificationBell.tsx)
- [Audit view](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/c1343a489ae603ac9d886d8096c4027e1b20c800/FE/src/roles/admin/audit-log/AuditLogView.tsx)
- [Revenue dashboard](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/c1343a489ae603ac9d886d8096c4027e1b20c800/FE/src/roles/owner/revenue/RevenueDashboard.tsx)
- [SPARK-335](https://mangamanagement.atlassian.net/browse/SPARK-335), [SPARK-338](https://mangamanagement.atlassian.net/browse/SPARK-338), [SPARK-340](https://mangamanagement.atlassian.net/browse/SPARK-340)

**Review limitation:** Source reviewed, not a running application. Uncommitted teammate work and live PostgreSQL/broker/browser outcomes are unverified. Proposed FE sequencing, estimates and minimal fallback UI require team approval. No repository or Jira mutation made by this handoff.
