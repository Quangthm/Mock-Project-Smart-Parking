# Operator layout WebSocket investigation

Date: 2026-11-13. Scope: FE implementation and authenticated browser runtime inspection only. No backend source, database or backend code was changed for this investigation.

## Confirmed runtime findings

Tested the existing authenticated Operator session on `http://localhost:5173/dashboard/operation`, assigned to site `b0000000-0000-4000-8000-000000000001`.

- Negotiate succeeded (HTTP 200) and advertised WebSockets, SSE and Long Polling.
- Browser WebSocket handshake was rejected with HTTP 403; browser close code was 1006.
- SSE also returned HTTP 403. The previous unrestricted transport configuration then fell back to Long Polling.
- The fallback received a SignalR close frame (type 7): `Connection closed with an error. HubException: INVALID_SITE_ID` despite a syntactically valid UUID in the path.
- One observed startup attempt generated three negotiate calls, four transport GETs, two transport POSTs and a DELETE, plus preflights. This is transport activity, not ten parking-layout snapshot API reads.
- A bounded comparison using the same token and endpoint found transport GET with query `access_token` returned HTTP 403, while transport GET with an Authorization bearer header returned HTTP 200. Each diagnostic connection was cleaned up. Tokens and connection tokens were not recorded in this report.

The query/header result identifies a server-side transport authentication/authorization compatibility gap to investigate; it does not identify the exact middleware/configuration defect without backend investigation. The `INVALID_SITE_ID` close frame is a separate server-side validation result requiring investigation of route binding/site resolution. Do not guess a new frontend route or fabricate authorization to conceal either failure.

## FE call path and remediation

`OperatorDashboard` selected-site effect -> `watchOperatorLayout` -> SignalR `connection.start()` -> negotiate/transport selection. The model's animation/render timer does not create this connection. `accessRealtime` uses a different endpoint (`/hubs/operators`) and its AppContext subscription is currently Owner-only.

Changes:

- WebSocket-only layout connection; no automatic fallback to HTTP polling.
- Three automatic attempts per subscription, shared cooldown checks for focus/visibility/auth refresh triggers, exponential delay with jitter, and terminal handling for recognized permanent/nested errors.
- Serial connection shutdown/start on replacement, preventing StrictMode/remount overlap.
- Visible unavailable banner and manual `Retry live updates`; keep the last snapshot without claiming it is realtime.
- Preserve event coalescing, trailing invalidation and affected-site snapshot reads from the earlier request reduction work.

## Verification

- After a clean reload of the patched FE, observed exactly three negotiate fetches and three failed WebSocket attempts in the first 55 seconds. No SSE fallback or Long Polling was created by the subscription.
- Opening Lot Status & 3D Map rendered the model without increasing WebSocket connection count (3 before, 3 after).
- A separate 62-second idle observation after retry exhaustion recorded 0 new layout transport fetches and 0 new WebSockets. The Operator remained signed in. This interval excludes the explicit authentication-comparison diagnostic requests; an earlier idle observation interrupted by page reload was discarded.
- Automated tests verify WebSocket-only options, cooldown/budget under repeated focus/auth/visibility events, nested 403 failures, serialized replacement cleanup, event bursts and trailing snapshot handling.
- Full frontend Node test suite: 245 passed, 0 failed.
- `npm run build`: passed, with existing bundle-size warnings.
- `npx tsc --noEmit`: blocked by existing missing Vitest/Testing Library/MSW dependencies and related errors under `src/test`; no production-source diagnostic was reported.

## Remaining backend/runtime handoff

1. Resolve HTTP 403 for browser WebSocket/SSE transport authentication using query access tokens at `/ws/parking-lots/{siteId}/layout`; verify legitimate Operator permissions rather than relaxing authorization.
2. Resolve `INVALID_SITE_ID` for the actual assigned UUID; confirm the hub route parameter contract/site binding.
3. Retest an authorized WebSocket returning HTTP 101, hub handshake completion, snapshot/event delivery, reconnect reconciliation and denied-site behavior.

The FE request storm is contained, but realtime delivery is NOT verified operational while the server continues to reject the connection. WebSocket-only is containment, not a backend-authentication fix. No new polling fallback was introduced.
