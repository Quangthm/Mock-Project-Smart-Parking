# SignalR access revocation — frontend report

## Final FE-only verification — 2026-11-02

No production code was changed during this verification. Commands were rerun against the current working tree.

| Check | Result | Evidence |
| --- | --- | --- |
| `npx tsc --noEmit` (from `FE`) | **PASS** | Exit 0, no diagnostics. |
| `npm run build` (from `FE`) | **PASS** | Exit 0; Vite reports an existing large-chunk warning. |
| Focused SignalR/auth/AppContext/Sign In/Operator live tests | **PASS** | `node --test` on `access-realtime`, `auth-context`, `auth-api`, `sign-in`, and `operator-live-races`: 90/90 passed, exit 0. |
| Broader run including `operator-provisioning.test.cjs` | **FAIL** | 97/107 passed, exit 1. All ten failures are in `operator-provisioning.test.cjs` and stop at its mock loader before feature assertions. |
| Browser console/network | **NOT VERIFIED** | `http://localhost:5173` refused connection in this execution environment. No app or container was started. |
| Backend-dependent lock E2E | **BLOCKED BY BACKEND** | Checked-out UserService maps `/hubs/operators` only (`src/Services/UserService/SmartParking.UserService.API/Program.cs:132`); `/hubs/access` and server event remain absent. |

### Coverage and reproduced failures

- **VERIFIED by automated tests:** login session creation, token refresh, logout, HTTP 401 cleanup and stale response handling in `auth-api.test.cjs`; AppContext restore/logout/expiry behavior in `auth-context.test.cjs`; Sign In flows in `sign-in.test.cjs`; SignalR connection, initial failure retry, disconnect retry, token factory refresh, the three reason codes, duplicate event suppression, cleanup and independent mock tab instances in `access-realtime.test.cjs`.
- **PARTIAL:** `access-realtime.test.cjs` mocks the SignalR service callback, but does not mount AppContext and assert that an `AccessRevoked` event removes both storage tokens, clears the User, and renders Sign In. Source inspection shows those operations at `src/context/AppContext.tsx:260–270` and `src/lib/authApi.ts:122–129`; this is **not** an automated end-to-end UI PASS. React StrictMode double-mount and stale pending `start()` completion are guarded in source (`src/lib/accessRealtime.ts:18–46`) but are **NOT VERIFIED** by a StrictMode render test. No duplicate connection was observed in the service mock; no browser claim is made.
- **PARTIAL:** AccountSwitcher exposes role exploration as a preview (`src/components/account/AccountSwitcher.tsx:236`); it does not itself perform a role switch. The current tests do not prove Driver↔Owner or Operator→Driver authenticated transitions for real accounts. Admin navigation is covered only by general auth source review, not a live browser session.
- **FAIL:** `tests/frontend/operator-provisioning.test.cjs:18` rejects current component imports not registered in its fixture. Reproduce with `node --test tests/frontend/operator-provisioning.test.cjs`. Failures begin at tests on lines 167, 185, 207 (two cases), 229, 247, 309, 328, 337 and 345. Reported missing imports include `../../../lib/uiToast`, `../../../components/icon/UntitledIcon`, `react-dom`, `../parking-lots/LotManagement`, and `../dashboard/BackupCapacityPanel`. This is a test-harness mismatch; no production failure is demonstrated by those errors. Per the verification request, no fix was made.

### Follow-up requiring approval

To close the mocked AppContext coverage gap, extend the auth-context test fixture to rerender effects and inject `AccessRevoked`, then assert token clearing, one redirect, cleanup and StrictMode remount behavior. To restore the broader suite, update the Operator provisioning fixture's import map and rerun its assertions. No production-code change is justified by the evidence above. Manual browser and backend lock E2E checks remain pending until an authenticated environment and `/hubs/access` are available.

## Status

**PARTIAL — blocked by backend for end-to-end validation.** The frontend now connects to the agreed general hub contract and performs local forced logout on `AccessRevoked`. The checked-out UserService currently maps only `/hubs/operators`; `/hubs/access`, `AccessRevoked` emission, and bearer handling for that hub were not found. No backend files were changed.

## Regression stabilization

| Item | Status | Root cause and resolution |
| --- | --- | --- |
| Grouped number input TypeScript error | FIXED | Two `onBlur` props on one input; combined selection reset and step rounding in one handler. |
| Operator status filter TypeScript error | FIXED | `SlotStatusFilter` was used without its exported type import; imported it from `useLotStatus`. |
| Owner site notice TypeScript error | FIXED | `setMessage` existed inside `useSiteManagement` but was not returned to `SiteManagement`; exposed it. |
| Owner operator site reassignment | PARTIAL | FE called nonexistent `operatorsApi.reassignSite`; UserService has no reassignment endpoint. The confirmation action is disabled with an explanation; existing assignments remain unchanged. |
| AppContext expiry tests | FIXED | Two tests expected Sign In although the existing token-expiry flow goes to landing. Expectations now match production; SignalR revocation still goes to Sign In. |
| Sign In and Operator test harnesses | FIXED | Their module mocks omitted existing `uiToast` (and Operator `formatStatus`) imports; fixture mappings were added without changing production imports. |
| SignalR mock coverage | VERIFIED | Added both remaining reason codes, fresh token on reconnect, temporary disconnect, duplicate events, and independent tab connections. |

No backend, database, authentication API contract, or role permission was modified.

## Implementation checklist

- [x] Reuse `@microsoft/signalr`, `VITE_API_BASE_URL`, `ensureAccessToken`, and `authApi.clearSession()`.
- [x] Start one connection from AppContext when authentication is ready and a user is present; stop on logout, account changes, and cleanup.
- [x] Use normal SignalR negotiation and automatic reconnect. Retry an initial connection failure without logging out.
- [x] Handle `USER_LOCKED`, `ACCOUNT_DISABLED`, and `SESSION_REVOKED` with safe English messages. Duplicate delivery is ignored.
- [x] Clear local tokens and authenticated state, close dashboard UI, and show Sign In without calling `/logout`.
- [x] Preserve existing protected HTTP 401 handling and the role-specific `/hubs/operators` connection.
- [ ] Verify a real lock event for Driver, Owner, Operator, and Admin — **BLOCKED BY BACKEND**.

## Files

| File | Change |
| --- | --- |
| `src/lib/accessRealtime.ts` | Dedicated authenticated SignalR connection, reconnect, event handling, cleanup. |
| `src/context/AppContext.tsx` | Owns connection lifecycle and forced local logout for all four roles. |
| `src/App.tsx` | Returns nested dashboard paths to the existing Sign In view after revocation. |
| `tests/frontend/access-realtime.test.cjs` | Focused token, duplicate-event, outage, and cleanup checks. |
| `tests/frontend/auth-context.test.cjs` | Updates the existing test fixture for current imports and browser history. |

## Verification

| Case | Result | Evidence / limit |
| --- | --- | --- |
| SignalR token factory, reasons, duplicate revocation, reconnect, independent tabs, cleanup | VERIFIED | `node --test tests/frontend/access-realtime.test.cjs`: 7/7 passed. |
| Existing auth API, AppContext, Sign In, and Operator tests | VERIFIED | Combined targeted run: 90/90 passed. AppContext alone: 9/9; before stabilization it was 7/9 (10/12 when combined with the three SignalR tests). Sign In/Operator harnesses previously failed before assertions. |
| Vite build | VERIFIED | `npm run build` exit 0; existing bundle-size warning remains. |
| TypeScript validation | VERIFIED | `npx tsc --noEmit` exit 0; before stabilization four errors were reported. |
| Real lock, reload, HTTP fallback, four-role navigation, multiple tabs | BLOCKED BY BACKEND / MANUAL QA | General access hub is absent; authenticated test accounts and live event are needed. |

## Backend dependencies and manual QA

The backend must expose authenticated `/hubs/access`, send `AccessRevoked` with `{ reasonCode }` only to affected sessions after the lock/revocation commit, and reject revoked sessions on protected HTTP APIs. Its JWT SignalR adapter must accept tokens on this path. The existing Owner-only `/hubs/operators` does not satisfy this contract.

Once available, sign in separately as each role, lock the active User from Admin, and confirm: the affected tab clears `sp_access_token`, `sp_refresh_token`, `sp_access_expires_at`, and `sp_mfa_recovery_remaining`; Sign In appears with the reason message; reload does not restore the session. Repeat with the realtime connection offline, then call a protected API to verify authoritative HTTP rejection. Confirm that an ordinary disconnect does not log out the user and that role switching starts only the current session's connection.

## Regression risks

The connection is re-created when AppContext receives a new User object, including a role change; cleanup stops the old connection. Initial hub failures are retried in the background and do not block login. Existing HTTP 401 behavior still uses its original session-expired notification and landing redirect. End-to-end event delivery, backend targeting, full browser role switching, and React StrictMode behavior remain **NOT VERIFIED** until live manual QA is possible. Site reassignment remains **BLOCKED BY BACKEND**; the FE does not pretend to save it.
