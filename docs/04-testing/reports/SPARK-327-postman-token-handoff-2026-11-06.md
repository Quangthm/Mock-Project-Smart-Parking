# SPARK-327 — Postman refresh-token handoff resolution

**Status:** Completed and accepted by the user on 2026-11-06. The user reports that all tests now pass and explicitly requests task/test closure. This is user-reported deployment evidence; a final Runner export and exact assertion count were not collected. No tokens, credentials or profile data are included here.

## Failure and supported diagnosis

The original collection passed login, public JWKS, unchanged-key restart, original-token acceptance, tampered-signature rejection and refresh-response checks. Request **09 Refresh rotates both tokens** returned new tokens, but **10 New access token works** returned HTTP 401 / `INVALID_TOKEN`.

The failure was isolated to the main collection's execution/token-handoff path. Revision 2 removes its dependence on reading a freshly issued token back through the saved environment during the same run. The user then observed request 10 returning 200 and `DIAGNOSTIC_ONLY`, followed by reporting that all tests passed.

**The exact original runtime mechanism is not proven.** We did not capture and compare the old failing request's on-wire token with request 09's response. A stale/replaced environment value is a plausible explanation, not a demonstrated fact. Local Secret Protection/Vault migration was observed by the user, but no evidence proves it caused the 401. The later absence of `sp_token_transport` from the visible environment does not retrospectively prove what happened to the access token. Ordinary `pm.environment.set/get` usage is not inherently invalid.

## How troubleshooting narrowed the failure

| Evidence | What it established |
| --- | --- |
| JWKS unchanged and original access token accepted after the user's restart | Stable signing-key loading worked in that run; regenerating keys was not indicated. |
| Refresh metadata assertions passed | A correctly shaped token response was returned; decoding alone did not prove protected-endpoint acceptance. |
| Separate GET with the saved fresh token returned 200 | The issued token could authenticate in that run; failure was specific to the main collection path. |
| In-memory HTTP test and disposable PostgreSQL integration test accepted refreshed access immediately | No universal backend refresh/persistence failure was reproduced in those controlled tests. |
| Explicit-header correction still failed in Desktop | Header interpolation alone was not a verified resolution. |
| Focused probe passed immediate explicit header, immediate Bearer helper and delayed header checks | Fresh response tokens worked through both auth methods; no evidence justified adding a delay. |
| User-exported collection scripts matched repository scripts exactly | The supplied shared scripts were not an outdated version. |
| Request 10 used No Auth, no saved Authorization header and empty request scripts | Those static request-level overrides were ruled out. This did not capture the transmitted request. |
| Revision 2 used the probe's direct response-to-run-local token handoff and passed in Desktop | The revised collection resolved the observed workflow failure. Exact Postman internals remain undetermined. |

## What changed in revision 2

Before, the shared post-response script saved each new token to the environment, and the next pre-request script read the environment again:

```javascript
// Request 09 post-response
pm.environment.set('sp_access_token', value.accessToken);
// Request 10 pre-request
const token = pm.environment.get('sp_access_token');
```

Revision 2 saves a dedicated run-local copy directly from the response and uses that copy for subsequent current-session requests:

```javascript
// Login/refresh post-response
pm.variables.set('sp_run_access', value.accessToken);
pm.variables.set('sp_run_refresh', value.refreshToken);
pm.environment.set('sp_access_token', value.accessToken);
pm.environment.set('sp_refresh_token', value.refreshToken);
// Next current-session request
const token = pm.variables.get('sp_run_access');
```

Control request 00 initializes these run-local values from saved checkpoints when starting a new run. Login/refresh responses replace them directly within that run. Environment values remain necessary for manual restart/MFA/expiry checkpoints. The original, tampered, consumed and expired-token tests still deliberately select their respective tokens; the fix does not substitute the current token into negative tests. Completion clears transient session values. The configured private key, backend token validation and session persistence were not changed to resolve this collection failure.

The collection has a distinct **v2** name/ID and a `verify_refresh` action that runs the actual **00 → 09 → 10** path without another restart. That action rotates the saved session once and stops at `DIAGNOSTIC_ONLY`; it never claims the full acceptance workflow passed.

## Verification and completion scope

| Check | Recorded result |
| --- | --- |
| Node workflow/probe regression tests | 14/14 passed, including simulated stale/protected environment values after refresh, correct old-token rejection, diagnostic routing and failure reporting. Simulations do not prove that Desktop produced those conditions. |
| Actual Postman CLI with synthetic loopback server, restart-continuation segment through expiry pause | 15 requests / 72 assertions passed; receiver confirmed refreshed bearer. |
| Actual Postman CLI with synthetic loopback server, `verify_refresh` | 3 requests / 23 assertions passed; receiver confirmed refreshed bearer. |
| User Desktop v2 diagnostic | User reported request 10 HTTP 200 and `DIAGNOSTIC_ONLY`. |
| User final acceptance | User reported all tests passing and requested closure on 2026-11-06. Final raw run evidence was not independently collected. |

The task and its Postman testing work are closed on that acceptance plus the recorded focused validation. This does **not** convert prior unrelated FacilityService fixture failures or skipped tests into passes. An enabled-MFA run was not separately evidenced; retain `NOT_EXERCISED` where applicable. Consumer JWT middleware, overlapping key rotation and production key storage remain outside this task's scope. Historical validation details and explicit deferrals are preserved in the [completed task record](../../../ai/tasks/completed/TASK-SPARK-327-SESSION-20261104.md).

## Prevention and a shorter troubleshooting procedure

1. Chain fresh response values through dedicated run-local variables. Use the environment for configuration and checkpoints across runs. Initialize the run-local values explicitly; do not rely on similarly named variables from another scope.
2. Keep one Authorization owner: the shared pre-request script. Keep request auth at No Auth and request-level scripts/Authorization rows empty for this collection.
3. Test the actual request path and negative cases. Retain the regression tests that simulate stale/protected saved values and verify that old/replayed tokens remain rejected.
4. When a refresh response passes but its next profile fails, compare a separate request and a small same-token probe before changing server code or repeating restarts. A same-run comparison is more discriminating than an arbitrary delay.
5. Record only status, failed assertion names and safe equality flags. Do not log tokens, export populated environments or disable Vault protection to make a test pass. An unresolved reference needs explicit handling, not silent string substitution.
6. Distinguish a run-time variable write from persistence into the environment editor. Keep variable values must be enabled; importing a new collection does not update an existing environment's template. If a safe diagnostic field is not visible after the run, do not invent its contents or infer token failure from its absence. A fresh imported template includes the field; existing users may add a blank `sp_token_transport` row before a future diagnostic. Do not re-import a blank environment over saved credentials/checkpoints or rerun solely to recover an optional report after acceptance.
7. Use a clearly named/versioned collection and a new run configuration after script changes. Return to `start_over` for full acceptance after a diagnostic has rotated the session. Reserve `COMPLETE` for the full workflow; `DIAGNOSTIC_ONLY` is a partial result.

See the [current testing guide](../postman/SPARK-327-JWT-JWKS-guide.md) for setup and manual checkpoints. If the problem returns, preserve the failure and capture safe evidence from the actual Desktop execution before claiming a specific Postman/Vault cause.
