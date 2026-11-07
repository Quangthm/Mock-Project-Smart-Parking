# SPARK-327: JWT/JWKS Postman verification

This is the shared manual test procedure for the stable UserService signing-key refactor. It uses an existing local test account and takes about 20 minutes, including a 15-minute access-token expiration wait. No private key belongs in Postman.

**Acceptance recorded 2026-11-06:** the user reports all tests passing with revision 2 and has accepted task/test closure. See the [completed testing and token-handoff resolution report](../reports/SPARK-327-postman-token-handoff-2026-11-06.md). This dated result does not automatically pass future runs, optional MFA branches or unrelated repository tests.

## Files and prerequisites

- Import [the collection](SPARK-327-JWT-JWKS.postman_collection.json) and [the blank environment template](SPARK-327-Local.postman_environment.example.json).
- Use Postman Desktop's local Collection Runner with support for `pm.execution.setNextRequest` and `pm.execution.skipRequest`. Do not use a cloud runner for localhost.
- Run the current UserService build with its database and a stable signing key already configured. Follow [Local setup §5.5](../../../LOCAL-SETUP.md) for key generation/configuration and the service runners. The expected public configuration is documented in [the JWKS contract](../../03-design/api/userservice-jwks-api-design.md).
- Use an existing, active local test account with a working email/password login. Complete any registration approval/activation first. If MFA is enabled, have the authenticator available. The script does not register users or change account/MFA settings.
- Have a way to restart **only UserService**, with the same signing key, key ID and database. Make sure your computer clock is correct.

The collection creates two login sessions, rotates their tokens and logs them out. It reads only that user's own profile and the public JWKS. It does not restart containers, recreate databases or change keys. An interrupted run can leave a test session active until expiry or normal session revocation; `start_over` discards saved client tokens and cannot log out an already-lost session.

## 1. Import and configure

1. In Postman, choose **Import**, select both JSON files above, and import them. Keep the request names and scripts unchanged.
2. Select the environment named **SmartPark SPARK-327 LOCAL - fill local values only** in the environment selector.
3. Open that environment and set the following **local values** (called **Current value** in some versions). Keep shared values blank for credentials. Never publish or export a populated environment.

   | Variable | Default / value to enter |
   | --- | --- |
   | `sp_base_url` | `http://localhost:5035`, or your actual local UserService port; no trailing slash. Only HTTP localhost/127.0.0.1 targets are accepted. |
   | `sp_jwks_path` | `/.well-known/jwks.json`, matching `SMARTPARK_JWT_JWKS_PATH`. |
   | `sp_issuer` | `SmartParking.UserService`, matching `SMARTPARK_JWT_ISSUER`. |
   | `sp_audience` | `SmartParking`, matching `SMARTPARK_JWT_AUDIENCE`. |
   | `sp_key_id` | `smartpark-local-01`, or your actual configured `SMARTPARK_JWT_KEY_ID`. This is public metadata, not the private key. |
   | `sp_email` | Your existing local test account's email. |
   | `sp_password` | That account's password; enter locally and retain the secret type. |
   | `sp_action` | `start` for the first run. |

4. Leave the other variables alone. The scripts maintain tokens, results and checkpoint state. A secret type masks a value in the UI; it is not a reason to share/export it. Do not paste JWTs into online decoding sites.
5. Select the **whole collection**, then **Run**. Select a local functional run with **all 24 requests selected**, `00 Control` first, and **Iterations = 1**. Do not run a single request with **Send**, because it does not execute the collection workflow.
6. In Runner settings, enable **Keep variable values** and **Stop run if an error occurs**. Use no iteration data file, no mock and no request delay. Enable **Turn off logs during run** and disable **Persist responses for a session** if available, to reduce local credential/token history. Use the same environment and settings for every continuation.

**Keep variable values is essential:** it saves checkpoint changes to local environment values between runs, without updating shared values. The workflow uses Postman's documented [request routing](https://learning.postman.com/docs/tests-and-scripts/running-collections/building-workflows/) and [Runner settings](https://learning.postman.com/docs/tests-and-scripts/running-collections/intro-to-collection-runs/).

## 2. Run the baseline; stop for the restart

Click **Start run**. It checks health, anonymous JWKS, login metadata and the protected profile. If MFA is required, follow the MFA instructions below, then return here.

Expected checkpoint: environment `sp_status = PAUSED_RESTART`. Read `sp_resume_hint`. Green assertions at this checkpoint mean only the completed stage passed; they do not mean the entire test is complete.

**A checkpoint ends the current collection run.** This is not the Runner's temporary Pause button. Continue by changing `sp_action`, then starting a **new whole-collection run**, with the same environment and saved local values. Rerunning while `sp_action = wait` just shows the current state.

## 3. Manually restart UserService, then continue

Only do this after `PAUSED_RESTART`, while the original token still has at least one minute remaining. Finish this step promptly; access tokens last 15 minutes.

For the standard Docker setup, run in PowerShell:

```powershell
docker restart smartpark-app-user-service-1
Invoke-RestMethod http://localhost:5035/health
```

If the health request fails while the service starts, retry the health request after a few seconds until it returns `status: ok`. Use your actual local port/container name if customized. For host/IDE execution, stop and start only the same UserService process using the same launch configuration; do not regenerate its key or rerun database recreation.

Do not run setup, replace the private key, change `kid`, clear the database or log in again during this restart checkpoint. Those actions would invalidate the test of the **original** token. Continuing is your confirmation that the restart happened; the script cannot itself prove an operating-system process restarted.

Set local `sp_action = continue_restart`, then run the whole collection again. It verifies:

1. Public JWKS `kid`, modulus `n` and exponent `e` are unchanged, and the original token still opens the protected profile.
2. A modified signature receives `401 INVALID_TOKEN`, while the unchanged token still works.
3. Refresh issues new access/refresh tokens and a new `jti` for the same account. The new access token works; the old access token and consumed refresh token are rejected. The rotated session still works after that replay attempt.
4. Logout makes both current access and refresh tokens unusable.
5. A separate login creates an untouched session for the expiration test. MFA may pause here again.

Expected next checkpoint: `sp_status = PAUSED_EXPIRY`, with `sp_core_result = PASS`.

## MFA checkpoint, whenever encountered

At `PAUSED_MFA`, normal login tokens have not been issued for that challenge. Enter a fresh six-digit authenticator code in local `sp_mfa_code`, set `sp_action = continue_mfa`, and run the whole collection again before the challenge expires. The code is cleared after use. The collection resumes the correct baseline or expiry stage.

If the challenge expires, use `start_over` for a new login instead of repeatedly trying codes. The script does not enable MFA or consume recovery codes. `sp_mfa_result = NOT_EXERCISED` means this account did not require MFA; it is **not an MFA pass**. If MFA regression evidence is required, run the procedure with an already-enabled local test account as well, and retain both sanitized results.

## 4. Wait for real expiration, then continue

Read `sp_expiry_ready_at`: an ISO timestamp in **UTC**, two seconds after the token's actual `exp`. Wait until that time, usually about 15 minutes. Leave this Postman session untouched: do not refresh it, log it out, edit the saved JWT, change keys or change the account's status/roles/password. Keep UserService running.

Then set local `sp_action = continue_expiry` and run the whole collection again. If you continue too early, it safely returns to `PAUSED_EXPIRY` with the same saved tokens; set `continue_expiry` again once the time arrives.

The final stage checks that JWKS is unchanged, the untouched expired access token is rejected, and its still-valid refresh token restores access for the same identity. It logs out that test session and clears saved passwords, challenge values and tokens.

Expected result: **`sp_status = COMPLETE`**, `sp_core_result = PASS`, and no failed assertions in any stage. This is completion of this JWT smoke workflow, not the full repository test suite.

## 5. Record evidence and handle failures

### Revision 2: verify the refreshed-token handoff without restarting

Use the collection named **SmartPark SPARK-327 - JWT and JWKS checkpoints v2**. It has a distinct collection ID so an older imported copy remains distinguishable. Revision 2 keeps access/refresh tokens returned by login or refresh in run-local variables for subsequent requests. The environment still saves checkpoints between runs; original/expired-token rejection tests retain their deliberately selected tokens. This follows the same response-to-run-local handoff as the successful Refresh Probe. See [Postman variable scope](https://learning.postman.com/docs/tests-and-scripts/write-scripts/postman-sandbox-reference/pm-variables). The earlier Desktop 401's exact cause remains unconfirmed; this change is not proof that Vault caused it.

For a saved session from the failed main run or successful probe:

1. Import the updated [collection JSON](SPARK-327-JWT-JWKS.postman_collection.json). Open the collection whose name ends in **v2**. Retain the existing LOCAL environment; do not import the blank environment template again.
2. In that environment, change only `sp_action` to **`verify_refresh`**. Leave tokens and other checkpoint values untouched. Keep Secret Protection enabled.
3. Run the **whole v2 collection** with one iteration, **Keep variable values** enabled and no iteration data. Do not send request 10 individually or rerun an old run configuration.
4. The control routes **00 → 09 → 10**, refreshing the saved session once and checking its new access token using the same scripts as the full workflow. No restart, login or password is required. The old access token may already be expired, but the saved refresh token must still be usable.
5. Expected: step 10 HTTP 200 and `sp_status = DIAGNOSTIC_ONLY`. Share only `sp_status` and **`sp_token_transport`** if available. The script writes this field to the run's environment; visibility/persistence in the saved environment editor must be verified separately. It contains version, token-source label, equality booleans and HTTP status; no credentials or identity fields. `environmentMatchesSelected = false` shows a saved-environment difference at request preparation. Header equality describes the prepared request object, not independent on-wire capture.
6. If 00 or 09 fails, stop and report its failed assertion/status rather than changing keys, disabling Vault or restarting. A missing/unresolved checkpoint or expired/revoked refresh token cannot be repaired by the diagnostic. If 10 fails, share its HTTP status and `sp_token_transport`.

This diagnostic rotates the saved session and stops before replay/logout/expiry tests. It deliberately leaves `sp_core_result = PENDING`; it does not attest a restart or mark the task complete. After the result is reviewed, use `start_over` for the full acceptance workflow and its manual restart/expiry checkpoints.

If `sp_token_transport` is absent from an existing environment, do not re-import the blank environment or invent a report. The latest template declares it, but importing the collection alone does not update an older environment template. You may add a blank default-type row with that exact name before a future diagnostic, then verify Keep variable values. Its absence after a run is not evidence that authentication failed. Do not repeat a successful acceptance run solely to recover this optional diagnostic report.

### Earlier request-10 header correction (superseded by revision 2)

If request 10 returned 401 but a separate request using `{{sp_access_token}}` returned 200, the saved refreshed token is usable. The failing collection request differs from the working request; the screenshots alone do not identify the exact Postman header/variable-resolution cause.

The revised collection removes any existing Authorization header and writes the selected token directly into the request header in its pre-request script, using Postman's documented [request header API](https://learning.postman.com/docs/tests-and-scripts/write-scripts/postman-sandbox-reference/pm-request). It no longer uses a `Bearer {{sp_bearer}}` header template. This also preserves deliberate old/tampered/expired-token checks.

That header-only correction did not resolve the user's Desktop failure. Use revision 2 above; do not repeat the obsolete header-only restart procedure. Requests still use **No Auth** because the shared script sets the header, with no additional request-level auth/scripts. The [resolution report](../reports/SPARK-327-postman-token-handoff-2026-11-06.md) explains the evidence, final change and prevention rules.

### Persistent request-10 failure: no-restart diagnostic

If request 10 still fails after the header correction, stop repeating the full restart cycle. Import [SPARK-327 Refresh Probe v1 - NO RESTART](SPARK-327-Refresh-Probe.postman_collection.json) as a **separate collection**. Keep the same local environment containing `sp_refresh_token` from the failed run. Do not re-import the blank environment.

Run all four requests once, with **Keep variable values** enabled, no iteration data, no extra request delay and no custom auth/scripts. The probe refreshes that saved test session once, then sends the **same new token** using an explicit header, Postman's Bearer auth helper, and an explicit header again after two seconds. No service restart, password, private key or new registration is needed. A 401 assertion intentionally does not prevent the remaining comparisons; a failed refresh stops the probe.

Afterward share only the local environment value **`sp_refresh_probe_report`**. It contains step names, HTTP statuses, sanitized error codes, remaining lifetime, server Date and a prepared-header comparison flag; no JWTs, credentials or profile fields. `preparedHeaderMatches` describes the script's request object, not independent packet capture. The report helps separate auth-helper differences from immediate-versus-delayed behavior; it does not by itself prove the underlying cause.

The probe updates `sp_access_token`/`sp_refresh_token` after its single rotation and marks the main workflow `DIAGNOSTIC_ONLY`. It leaves the session available for diagnosis. Once the issue is resolved, use `start_over` for the full workflow; the probe cannot mark SPARK-327 complete. If it reports a missing refresh token or P0 rejects refresh, report that result rather than restarting services or manually changing checkpoint state.

If **all probe requests pass while the main collection still fails at 10**, retain both results and inspect the actual failing imported main collection before changing the server or repeating the restart cycle. Export that collection's JSON (not its environment or a run export), remove saved response examples and any literal passwords/tokens if present, and provide the sanitized definition to the reviewer. Compare collection/folder/request scripts, auth settings, headers and variables with the repository version. Passing immediate probe requests is not evidence that an arbitrary delay is the fix.

Where Export is available: Collections → the main collection's three-dot menu → More → Export collection → Export JSON. Older versions may expose Export directly and offer Collection v2.1. Keep any variable/Vault references intact; exclude actual secret values. See [Postman's export instructions](https://learning.postman.com/docs/getting-started/importing-and-exporting/exporting-data).

If More contains only **Generate specification**, do not generate a specification or publish the collection merely to obtain an export. Copy the collection's Scripts → Pre-request and Post-response editors into separate text files instead. Review for literal secrets before sharing; preserve variable references. This captures only the collection scripts, not the full request definition. Also inspect **request 10 itself**: Authorization should be **No Auth**, there should be no saved Authorization header, and both request-level script editors should be empty. Do not remove the shared collection scripts. Report the selected auth type, presence of an Authorization header (without its secret value), and whether request scripts exist. Matching collection scripts alone cannot rule out request-level overrides.

**Local Secret Protection diagnostic:** a notification that a secret was moved to Local Vault is Postman protecting a detected credential, not a UserService error. Automatic protection can replace literal values with secure variable references. This is a possible explanation for differences between persisted environment reads and the probe's run-local token chaining; confirm the actual exported definition/reference chain before calling it the cause. Inspect only whether `sp_access_token` / `sp_original_access` or related collection variables contain references; never share resolved Vault contents or the Vault key. Preserve these references in the export. Do not disable protection or move credentials into shared values as a troubleshooting requirement. Per-secret plaintext overrides and organization policies vary by plan; see [Local Secret Protection behavior](https://learning.postman.com/docs/administration/managing-your-team/secret-scanner/how-secret-scanner-works).

### Recording a run

Record the tested Git revision/build, local setup (Docker or host), date, confirmation of the actual restart, `sp_status`, `sp_core_result`, `sp_mfa_result`, and the JSON text in **`sp_results`**. The results contain check names and timestamps without tokens or account identifiers. Store sanitized evidence under `docs/04-testing/evidence/`, or send those fields to the reviewer. Do not share passwords, codes, environment exports, full response bodies or unreviewed Runner reports/screenshots; those may contain tokens.

| State / symptom | What to do |
| --- | --- |
| `PAUSED_RESTART`, `PAUSED_MFA`, `PAUSED_EXPIRY` | Perform the named manual action; set the matching `continue_*` action; run the whole collection again. |
| `FAILED` | Stop. Read `sp_resume_hint`, failed assertion names and HTTP error code locally. Correct the cause before an explicit `sp_action = start_over`. Refill the password if it has been cleared. |
| Network/script error with `RUNNING` left behind | This can occur before the post-response script runs. It is not a pass. Restore connectivity/Runner support, then use `start_over`. |
| Health connection refused | UserService is stopped, starting, or on a different port. Confirm its health before testing. |
| Login rejection | Check that this account is active and its credentials are correct. There are no deliberate wrong-password tests or automatic retries; avoid repeated attempts that could lock the account. |
| Step 09 passes but step 10 returns `401 INVALID_TOKEN` | The refresh response passed metadata checks, but acceptance of the new token failed. If steps 05/06 passed, same-key restart already succeeded. Do not infer a key configuration failure. Before the refreshed token expires, create a separate GET request **outside this collection** to `{{sp_base_url}}/api/auth/me`, select this environment, choose Bearer Token auth and enter `{{sp_access_token}}`. Use no extra Authorization header or scripts. A 200 isolates the issue to the collection's sent request/token selection; a 401 with a still-unexpired token requires investigation of server validation/session persistence. Record only HTTP status/error code and failed step; never share token values. If the token has expired, this diagnostic is inconclusive and requires a fresh baseline. |
| Saved tokens/checkpoint missing | Confirm the selected environment and **Keep variable values**. Restart the workflow; lost original tokens cannot prove restart persistence. |
| Step 00 passes health and "Paused for restart", then fails "JWT has three Base64URL segments" | The saved `sp_original_access` is missing or malformed; this is a local checkpoint failure before the protected endpoint is called, not evidence of server rejection or token expiration. Set local `sp_action = start_over`, retain your credentials, and start a **New Run** of the full collection with Keep variable values enabled. Let it reach PAUSED_RESTART and confirm `sp_original_access` is populated locally without copying/sharing it. Then restart UserService, set `continue_restart`, and start another New Run with the same environment/settings. Let the scripts manage `sp_status`, `sp_step` and token values. |
| Unknown `pm.execution` API / script error | Update/use a Postman Desktop version supporting the documented workflow APIs. Do not bypass the guards by sending individual requests. |
| Restart changes `n`/`e` or `kid` | Check stable key loading and startup configuration locally. A reused `kid` alone does not prove the key is unchanged. |
| Original token too close to expiry | Use `start_over` and perform the restart promptly during the next baseline. |
| Expired-token check unexpectedly returns 200 | Confirm the host/server clocks and actual time. Retain the failure; do not call it a pass or mask it by editing the token. |
| Need a fresh run after completion | Refill local password, set `sp_action = start_over`, then run the whole collection. `start` deliberately does not overwrite an existing checkpoint. |

## Coverage and completion limits

The collection covers the running UserService's public JWKS contract, configured `alg`/`kid`/issuer/audience, identity/session claims, 900-second access lifetime, same-key restart, signature tampering, refresh/replay, logout, natural expiration and optional MFA completion. The lifetime assertion allows a one-second rounding difference because issuance and response sample the clock separately. It does not use the frontend, so frontend automatic refresh cannot hide an expired-access-token rejection.

It decodes JWT metadata and asks the real protected endpoint to accept/reject tokens. It does **not independently verify a signature using the downloaded JWK**. Cryptographic verification and invalid-key/startup combinations belong to [JwtJwksTests](../../../tests/Services/SmartParking.UserService.Tests/JwtJwksTests.cs):

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --filter FullyQualifiedName~JwtJwksTests
```

Consumer-service JWKS middleware, overlapping key rotation and production secret storage remain outside SPARK-327's implementation scope. Account administration, all MFA enrollment/recovery cases, database integration and broker behavior are not proved by this collection. See [automated integration-test setup](../../../tests/README.md) for the separate database/broker checks.

Mark this Postman workflow complete only after all stages succeed and the restart actually occurred. Move SPARK-327's overall task record to completed only after its remaining verification gaps are resolved or explicitly accepted/deferred; a green partial run must not close the task.

## Maintaining the collection

The committed JSON is the runnable source; the reusable scripts are stored once at collection level under **Scripts**. No extra package or downloaded script is required. Keep the request numbering/names synchronized with `sp_request_names`, and rerun the local workflow harness when changing scripts:

```powershell
node --test tests/postman/spark327-collection.test.cjs
```

That harness uses synthetic responses and an adjustable clock to check checkpoint, failure and credential-handling behavior. It does not contact UserService or establish live JWT correctness. Follow the manual procedure above for deployment evidence.
