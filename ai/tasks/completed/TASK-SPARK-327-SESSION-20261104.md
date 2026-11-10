# Session Review — 2026-11-04

## 1. Session Information
- **Session ID:** TASK-SPARK-327-SESSION-20261104
- **Date:** 2026-11-04 (Asia/Saigon)
- **Agent:** Codex with delegated JWT, configuration, and documentation work
- **Branch:** feat/SPARK-327-userservice-jwt-jwks
- **Related Task:** SPARK-327 — stable JWT configuration and UserService JWKS
- **Status:** COMPLETED — user reports all tests passing with v2 and explicitly accepts task/test closure on 2026-11-06. Completed scope includes stable JWT/JWKS, Postman verification, consumer guidance and startup checks. Exact historical Desktop 401 mechanism remains unproven; unrelated suite failures/skips and out-of-scope consumer middleware are not marked passed.

## 2. Objectives
- [x] Preserve existing authentication/session behavior; add stable RS256 signing configuration and public JWKS.
- [x] Isolate signing secrets to UserService across setup, host processes, and Docker.
- [x] Verify configuration workflows, focused security regressions and user-reported Postman acceptance; document operations, evidence limits and deferred work.

## 3. Work Completed
- [x] Baseline working tree clean; HEAD `3d3def763870f66f8da71386357a66f776fa61e5`.
- [x] Read router, coding/environment/session conventions and canonical security/authorization context; delegated complete scoped audits.
- [x] Delivered initial source audit and file-by-file modification plan to user before implementation.

### Initial source audit (historical; subsequent validation recorded below)
| Component | Existing behavior | Evidence | Required correction |
|---|---|---|---|
| JWT issuance | RS256, 900 seconds; sub=UserId, account_id=AccountId, role=current account role, jti=AccessTokenId | LoginCommandHandler → AuthSessionService.CreateAsync/Response → IAccessTokenService → AccessTokenService | Preserve issuance; add kid |
| Key provider | Raw PEM; RSA >=2048; private-key check; Development ephemeral fallback | JwtKeyProvider, Program eagerly resolves provider | Base64 full PEM, matching input checks, stable kid, safe validation |
| JWT config — resolved | Before SPARK-327, Development used different Demo issuer/audience names | appsettings.json and appsettings.Development.json | Defaults now match: issuer `SmartParking.UserService`, audience `SmartParking`; explicit environment mappings implemented |
| AuthSession/current access | Hashes refresh, rotates identifiers/tokens, validates local authoritative session/account/role | AuthSessionService, CurrentAccountBearerEvents, HttpCurrentActor | Preserve and regress |
| setup.ps1 | Missing copies template; O/K/A; default/noninteractive Keep; overwrite .env.bak; stdin desired-map synchronization | setup.ps1; scripts/test-setup.ps1 | Add scoped JWT mapping; retain workflow |
| Host | Export-all loader leaks raw secrets and parent config aliases into other children | load-env.ps1; run-schema-service.ps1 | Filter private JWT aliases and explicitly project to UserService |
| Docker | Raw PEM already only UserService; no consumer trust settings; root context unfiltered | compose.app.yml; docker/Dockerfile.backend | Public trust mappings; .dockerignore |
| JWKS | Absent | UserService route/source search | Anonymous public-only active-key publication |
| Tests/contracts | Auth/session and TestHost infrastructure exist; collection incorrectly asserts 3600 seconds | AuthSessionTests, UserServiceDatabaseFixture, UserService.postman_collection.json | Focused JWT/JWKS tests; isolation/setup cases; fix collection |

## 4. Technical Decisions
- Approved user task chooses UserService as sole RS256 issuer and JWKS publisher; consumers only receive trust configuration in this task.
- A nonempty KeyId declares shared/stable configuration and requires a configured private key even in Development. Unconfigured standalone Development retains ephemeral behavior.
- Legacy raw PEM remains supported. Without an explicit KeyId, a configured key uses a deterministic public-key thumbprint. Two populated key forms must identify the same RSA key.
- Public JWKS uses `/.well-known/jwks.json` by default, with five-minute cache lifetime. Only active key is published; multi-key rotation overlap is deferred.
- Original named 2026-11-04 decision documents were adopted into canonical ADR/matrix with explicit provenance. These supersede old blanket cross-service current-session policies; UserService local checks remain mandatory.
- Existing architecture inventory has historical entries (e.g. broker/payment absent) contradicted by current source; use inspected source rather than stale inventory for actual implementation.
- No packages, user .env changes, database recreation, commits, pushes, PRs, or shared infrastructure changes authorized/needed.

## 5. Files Changed
| Files | Change / compatibility and security effect |
| --- | --- |
| `src/Services/UserService/SmartParking.UserService.Application/Common/Models/JwT/JwtOptions.cs` | Adds Base64 PEM, key ID and publication path options; raw PEM retained. |
| `src/Services/UserService/SmartParking.UserService.Infrastructure/Services/JwtKeyProvider.cs` | Validates both input forms; matching dual inputs only; stable thumbprint fallback; public key metadata; sanitized failures. Explicit key ID requires stable signing material. |
| `src/Services/UserService/SmartParking.UserService.API/Extensions/JwksEndpointExtensions.cs`, `Program.cs` | Anonymous public-only active JWKS; startup validation preserved; 300-second cache and ephemeral warning. |
| `src/Services/UserService/SmartParking.UserService.API/appsettings.Development.json` | Aligns issuer/audience with normal defaults; old Demo tokens need re-login unless explicitly configured. |
| `tests/Services/SmartParking.UserService.Tests/JwtJwksTests.cs` | 28 focused cases including actual HTTP login/refresh through production composition with in-memory persistence, restart key stability, cryptographic validation and invalid configuration. |
| `.env.example`, `setup.ps1` | Safe placeholders; User-only signing mappings; consumer public trust; optional stale secret removal; stdin sync maintained. |
| `scripts/jwt-environment.ps1`, `scripts/load-env.ps1`, `scripts/run-schema-service.ps1` | Filters raw/config/prefixed private aliases including inherited variables; explicit User projection and restoration; service aliases retained. |
| `compose.app.yml`, `.dockerignore` | User-only private inputs, consumer public trust; exclude local credentials/key/runsettings files from backend build context. |
| `scripts/test-setup.ps1` | Setup/runner isolation and placeholder tests, interactive branches in scratch checkout, backup and idempotence. Corrects obsolete Services:Facility expectations to current Services:Parking. |
| `QUICK_START.md`, `LOCAL-SETUP.md`, `UPDATE_SCRIPT_GUIDE.md`, `docs/07-skills/SmartParking_Env_Config_SKILL_v1.0.md` | Exact setup, manual 3072-bit generation, encoding, service configuration matrix, restart/rotation and technical-debt instructions. |
| `docs/03-design/api/userservice-jwks-api-design.md`, `docs/04-testing/postman/UserService.postman_collection.json` | Public contract and reusable anonymous JWKS assertions; stale 3600-second assertion corrected to 900. |
| This session record | Audit, decisions, evidence and handoff. |

No login, MFA, refresh, AuthSession, bearer current-account events, current actor, persistence, consumer middleware or Saga implementation was changed. The existing signing credentials automatically emit the provider's KeyId as `kid`.

### Configuration matrix
The complete variable → user-secrets → host → Docker → runtime mapping is in environment skill §8. All private PEM/Base64 mappings target UserService only. Other backend services receive `JwtValidation:Issuer`, `Audience`, `JwksUri`; host URI derives from configured UserService URL and path, Docker uses `http://user-service:8080` plus path. Frontend receives no JWT private settings. Setup skips placeholders, runtime rejects supplied malformed keys. Root loader behavior intentionally changes from export-all to filtered export.

## 6. Issues and Blockers
- **Historical verification gap / separate follow-up:** initial inspection lacked isolated PostgreSQL/runsettings and running Docker containers. A later focused disposable PostgreSQL refresh/profile test passed (recorded below), but the entire skipped database/MFA lifecycle/broker suite was not rerun. No separate enabled-MFA deployment evidence was supplied. These remain limitations of task acceptance, not successful checks.
- **Unrelated suite failures:** four FacilityService operational-layout fixture tests fail (expected metrics 8/24 versus 0 and fresh/stale fixture differences in slot updatedAt). Their source/fixtures are unchanged by this task; baseline rerun in a separate checkout not performed. No claim that the full solution suite passes.
- **DEFERRED:** consumer middleware/JWKS cache/refetch, production key store, overlapping key rotation, REQUEST Sagas, accepted stale-token/session and authorization-to-effect race debt.
- **Limitation:** a stateless key provider cannot detect historical reuse of an arbitrary configured kid for a different key; documentation must prohibit reuse and report this limitation.

## 7. Validation Results
**Final acceptance:** user reports all tests now passing and requests completion on 2026-11-06, following the explicit v2 request-10 HTTP 200 / DIAGNOSTIC_ONLY result. This closes the task's manual Postman testing work by user acceptance; final raw Runner output/count was not independently collected. See the [resolution and testing report](../../../docs/04-testing/reports/SPARK-327-postman-token-handoff-2026-11-06.md). The following initial automated results and later dated continuations retain their original scopes.

- `dotnet build SmartParking.slnx --nologo`: PASS, 0 warnings/errors, with SDK/cache access. Initial restricted attempt returned failure without diagnostics.
- `dotnet test SmartParking.slnx --no-build --nologo`: executed; UserService 245 passed / 54 skipped; FacilityService 108 passed / 4 failed / 13 skipped; PaymentService 88 passed; IoTService 1 passed. Full test result is FAIL due to Facility fixtures. Log `.cache/spark327-tests.log`.
- `dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-restore --nologo --filter FullyQualifiedName~JwtJwksTests`: PASS 28/28 after final provider fix. Log `.cache/spark327-jwks.log`.
- `C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-setup.ps1`: final PASS on Windows PowerShell 5.1.26100.9711, including real subprocess private-variable isolation for all consumer aliases, setup mapping/placeholder checks, Keep/Enter/Abort/Overwrite backup and idempotence. Log `.cache/spark327-setup-ps51.log`.
- `docker compose --project-name smartpark-app --env-file .env -f compose.app.yml config --quiet`: PASS; no rendered secrets printed and no containers changed.
- PowerShell parser check: 0 errors under 7.6.5; explicit Windows 5.1 harness separately executes changed scripts.
- Initial Postman collection JSON parsed; live execution was pending at that point. Later user-run deployment checks, controlled CLI runs and final acceptance are recorded below. No Postman workspace initialization/publication performed by the agent.
- `git diff --check`: PASS (line-ending advisory only). Private PEM marker scan over src/FE/docs/template: no matches.

## 8. Git Status
- **Branch:** feat/SPARK-327-userservice-jwt-jwks
- **Commit:** 3d3def763870f66f8da71386357a66f776fa61e5
- **Working Tree:** clean at start; implementation changes will remain uncommitted.

## 9. Handoff / Next Steps
- [x] Implementation and focused source/configuration review completed.
- [x] Build, focused tests, solution tests, setup harness and safe Compose validation executed.
- **Separate follow-up, outside completed acceptance:** resolve unrelated Facility fixture failures; broaden skipped database/MFA/broker coverage where required. Do not report the full solution suite as green.
- [x] User reports stable-key setup and successful account creation/login; private key remains local.
- [x] User reports all tests passing after the v2 fix and explicitly accepts completion; manual testing work closed. Optional branches without separate evidence remain unverified, not automatically passed.
- Suggested commit: `feat(auth): add stable UserService JWT signing and JWKS [SPARK-327]`.
- No commit, push, merge or PR was performed; HEAD remains unchanged.

## 10. Context for Next Session
**Closure supersedes historical pauses:** this record was moved to completed on 2026-11-06 at the user's request after reported successful tests. Dated PAUSED entries below are troubleshooting history, not current instructions to repeat diagnostics. Current operational steps live in the testing guide; the resolution report distinguishes the verified fix from the unproven original Postman mechanism.

2026-11-05 housekeeping: marked the historical JWT configuration finding resolved. Kept SPARK-327 active because verification gaps remain; the completed SPARK-248 audit was moved to completed without treating its proposed cleanup as implemented.
Follow the user request in pasted attachment and canonical router. Delegates own disjoint source/config/docs work. Do not read or print real private settings, render secret-bearing Compose output, start shared containers, or run fixture databases without established authorization. Targeted in-process test hosting and scratch setup harness are within test scope.

Current continuation: the earlier delegates are no longer active. Continue in this chat; no further delegation is required.

### 2026-11-05 container naming follow-up — preflight
User requested parking → facility and reservation → operation Docker names. Updated Compose service/image names, internal URLs, run/stop maps and setup guides; legacy DNS aliases retained. Compose quiet validation passed. Inspected exact local containers 08105dbaf6e9 and d08920bd9597: both healthy, no mounts. Plan: reuse their existing images under canonical tags, retain original containers stopped under explicit legacy names, launch only new facility/operation containers. No database/volume deletion or unrelated service restart. This reversible migration is within the requested naming change; original containers retained for rollback.

Container naming follow-up COMPLETE: `smartpark-app-facility-service-1` and `smartpark-app-operation-service-1` launched with canonical Compose labels/images and reported healthy via Compose --wait. From the unchanged PaymentService container, HTTP GET /health succeeded for facility-service, parking-service, operation-service and reservation-service. Original containers retained stopped as `smartpark-app-facility-service-legacy-20261105` and `smartpark-app-operation-service-legacy-20261105`; Compose will report these as orphans until explicit cleanup. UserService/Payment/Frontend were not restarted; databases and volumes untouched. Updated files: compose.app.yml, scripts/run-app-service.ps1, scripts/stop-app-service.ps1, QUICK_START.md, LOCAL-SETUP.md, UPDATE_SCRIPT_GUIDE.md. Existing JWT verification gaps remain separate. No commit/push.

### User-reported smoke verification — 2026-11-05
User confirms account creation and login work as intended with the configured deployment. This is user-reported manual evidence, not a replacement for skipped database-backed automated tests. Remaining deployment checks: anonymous JWKS/public fields and matching kid; unchanged-key restart with original token; refresh rotation and replay rejection; logout revocation; raw expired/tampered-token rejection without frontend auto-refresh; MFA login where enabled. Formal completion still requires resolution or explicit acceptance/deferment of the recorded validation gaps.

### Postman testing package and user checkpoint — 2026-11-05

User requested a runnable Postman script, step-by-step shared testing documentation, and explicit pauses for personal actions. Delivered:

- `docs/04-testing/postman/SPARK-327-JWT-JWKS.postman_collection.json`: 24 requests, shared assertions and guarded workflow. Baseline login/profile → PAUSED_RESTART → unchanged-key/original-token check, tamper, refresh/replay and logout → separate expiry login → PAUSED_EXPIRY → natural expiry rejection, refresh recovery and logout cleanup. MFA branches stop at PAUSED_MFA. The next whole-collection run resumes only with the corresponding `sp_action`.
- `docs/04-testing/postman/SPARK-327-Local.postman_environment.example.json`: blank credential/token values with secret types; public trust defaults match `.env.example`. No signing private key is used by the tests.
- `docs/04-testing/postman/SPARK-327-JWT-JWKS-guide.md`: prerequisites, import, local values, Runner settings, exact manual restart command, MFA/expiry continuation, failure recovery, safe evidence and scope limits. Linked from `docs/04-testing/README.md` and the JWKS API contract.
- `tests/postman/spark327-collection.test.cjs`: dependency-free Node workflow harness. Synthetic responses/clock verify routing, checkpoint retention, original-token usage, tampering, refresh token selection, MFA branches, expiry waiting, config/key changes, pre-request guards, lifetime rounding and credential cleanup. Synthetic results do not establish live API correctness.

Verification performed in this continuation:

- `node --test tests/postman/spark327-collection.test.cjs`: **PASS 8/8**, exit 0. No packages installed.
- Postman CLI **1.86.0**, no account/workspace initialized. Ran the real collection with its blank environment against `http://localhost:5035`, cloud event reporting disabled, redirects disabled, request/run timeouts and bail enabled. **2 actual HTTP requests, 12 assertions passed, exit 0**: health and anonymous public JWKS. Login was skipped before sending because no credentials were supplied. This is a partial read-only preflight, not a completed JWT smoke run. The first restricted attempt hit local socket EACCES; the approved retry reached the service. CLI emitted an unauthenticated cloud-publication notice; no cloud login/publication was performed.
- Postman's real pre-request/post-response runtime loaded the JSON and shared scripts successfully; the Node harness separately covers the not-yet-executed authenticated branches. Final lifetime-only refinement permits 899–900 seconds for separate clock sampling, and its regression case passed; the public preflight did not depend on that assertion.
- No app/container restart, account login, private-key access, populated environment export, shared workspace modification, commit or push performed in this continuation.

**PAUSED at initial user setup:** import the collection/environment in Postman Desktop, enter local test credentials, enable Keep variable values + Stop run if an error occurs, run the whole collection with one iteration, and report `sp_status` plus failed assertion names if any. Expected first manual checkpoint is PAUSED_RESTART (or PAUSED_MFA). Do not restart on the user's behalf. Retain this task in active until all live stages and the remaining separately recorded validation gaps have evidence or explicit acceptance/deferment.

Continuation evidence must distinguish `COMPLETE` for this smoke workflow from overall task acceptance. MFA `NOT_EXERCISED` is not a pass; require an enabled-account run if closing the MFA gap. Scripts decode metadata and test live acceptance/rejection; independent JWK signature validation and invalid configuration remain covered by the separate focused .NET tests. The public-only live check now resolves anonymous JWKS deployment evidence, but not restart/session/expiry behavior.

### User-run restart and refresh failure — 2026-11-05

User screenshots establish baseline 27/27 assertions passed, then continuation steps 05/06 passed (JWKS unchanged and original access token accepted after user-performed restart). Tampered-signature rejection, unchanged-token control and refresh response assertions also passed. Step 10 failed its HTTP 200 assertion; user supplied HTTP 401 / INVALID_TOKEN, traceId `d2004205f81f4cd1afe51b1217b15b95`. The second red assertion is the collection's stop marker, not a second independent backend failure.

Root cause remains OPEN: metadata validation of a refreshed JWT does not establish protected-endpoint acceptance. Reviewed collection token selection and current refresh/session persistence path; no confirmed defect from source inspection. Filtered Docker log inspection returned no matching request/status or security-exception identifiers, so no log-based cause established. No tokens/credentials obtained, keys changed, database accessed, or services restarted. Added a diagnostic procedure to the shared guide: separate protected GET with the saved fresh access token, outside collection scripting, before expiry. PAUSED pending that distinction; do not reset the signing key or mark overall task completed.

### Refresh rejection follow-up and header correction — 2026-11-05

User initially reported a separate 401 within 15 minutes. Subsequent fresh reproduction is more discriminating: collection step 10 returned 401 (trace `4b92925748b94924a48da05b54e7499e`), while a separate request with the same environment's `sp_access_token` returned a successful own profile. Do not record profile PII in evidence. This establishes that the saved refreshed token is usable in that run; exact imported-request/header/variable-resolution cause remains unobserved.

Verification and changes in this continuation:

- Extended `JwtJwksTests.HttpLoginAndAnonymousJwksUseTheSameConfiguredKeyAcrossHosts` to GET the protected profile with original and refreshed tokens. **PASS 1/1**, exit 0; production HTTP composition with in-memory storage. Initial restricted SDK execution failed without diagnostics; approved SDK retry passed.
- Added `IdentityAccessHardeningTests.RefreshedAccessTokenImmediatelyOpensProtectedProfileWithPostgres`. **PASS 1/1**, exit 0: login, profile, refresh, new-token profile and old-token rejection using the existing disposable PostgreSQL fixture. A process-local loopback-only test connection was derived using the repository env loader without printing credentials. Fixture created/dropped only its random `smartpark_auth_test_` database; application data and runtime services were not changed. No persistent runsettings file was created.
- Original collection tested with actual Postman CLI against a synthetic local HTTP fixture: **15 requests / 72 assertions passed**, exit 0; receiver confirmed refreshed bearer on the wire. This proves the CLI's controlled behavior, not the user's Desktop request or real backend. Synthetic server exited after the run; no cloud reporting or real account used.
- Corrected collection pre-request handling to remove stale Authorization and set the exact selected bearer directly via `pm.request.headers.upsert`; removed static `Bearer {{sp_bearer}}` headers. Updated Node harness to inspect request headers and inject stale header/local-token values. **PASS 8/8** after correction; focused stale-header assertions included. No backend authentication/session implementation changed.
- Added shared-guide import/update/retest instructions. An intermediate standalone diagnostic collection was withdrawn after the user's successful separate-request evidence made it unnecessary.
- Additional live Postman CLI validation was not executed: automatic approval review hit the account usage limit, explicitly a review failure rather than an unsafe-action determination. No bypass attempted. Corrected Desktop execution remains user verification, not a claimed pass.

**PAUSED:** user imports updated collection only, retains existing environment, sets `sp_action=start_over`, runs fresh baseline, performs restart at PAUSED_RESTART, then confirms step 10 and remaining expiry/logout stages. No signing key change or backend rebuild is needed for this collection correction. Keep overall task active until live completion and disposition of remaining verification gaps.

Latest screenshot follow-up: step 00 health/same-environment/PAUSED_RESTART assertions pass, but decoding `sp_original_access` fails the three-Base64URL-segments assertion. Only /health was requested; no evidence yet about the corrected request-10 behavior. Saved original token is absent/malformed; how it was lost remains unknown. Added guide recovery: start_over with New Run and Keep variable values, let scripts save a fresh baseline, verify token variable populated locally, then manual restart and continuation. PAUSED for that user-run baseline; no new backend changes or test execution needed for this documentation-only update.

### 2026-11-06 — persistent 401, focused probe, consumer guidance and startup safety

User still reports request 10 returning 401 / INVALID_TOKEN (trace `ed631f7f16984011a5ef07afa7ed9455`). The explicit-header correction is NOT a verified resolution. Do not prescribe another unchanged full restart cycle. User requested consumer key-retrieval documentation and startup safety, then explicitly selected **Guide and startup-safety checks**, excluding consumer JWT middleware implementation.

Delivered `docs/04-testing/postman/SPARK-327-Refresh-Probe.postman_collection.json` as a separate collection with a distinct name/ID. It reuses the saved refresh token, rotates once and compares the same fresh token through immediate explicit header, immediate Bearer auth helper and delayed explicit header. It produces only sanitized `sp_refresh_probe_report` metadata, updates local session tokens and marks the main workflow DIAGNOSTIC_ONLY. No password/private key/restart needed; failed refresh stops; profile 401 assertions remain failed while remaining comparisons execute. Prepared-header comparison is not packet capture and cannot alone prove the exact on-wire value. Shared testing guide documents usage and limits.

Validation in this continuation:

- Actual Postman CLI against synthetic loopback fixture: **4 requests, 4 assertions passed, exit 0**, including Bearer helper and two-second delay. Fixture receiver confirmed fresh bearer; no real service/account used, cloud reporting disabled. This controlled result does not resolve the user's Desktop/deployment failure.
- `node --test tests/postman/spark327-collection.test.cjs tests/postman/spark327-refresh-probe.test.cjs`: **11/11 passed**, exit 0. Added checks for diagnostic continuation on 401, safe report, missing token/remote target/wrong step guards, and failed refresh preserving saved session values.
- `node --test tests/startup/consumer-startup.test.cjs`: **3/3 passed**, exit 0. Starts current compiled Facility/Operation/Payment APIs on disposable loopback ports with dummy database settings and an unavailable UserService/JWKS fixture. All returned health 200, stayed running and made zero UserService startup calls. Child APIs and fixture servers cleaned up. Existing Docker containers and application databases not changed.
- `git diff --check`: PASS, existing line-ending advisory only.

Updated coding skill §4: public-only JWKS URL/configuration mapping (host versus Docker), signature/issuer/audience/algorithm/lifetime validation, plain JWKS versus OIDC discovery, current implementation boundary, future bounded cache/retry/shared refresh/atomic snapshot requirements, fail-closed protected requests without process crashes, readiness versus liveness, and required outage/concurrency tests. These are future consumer requirements, not a claim that middleware/caching exists. Current consumers receive settings only; Payment Development auth is not JWT verification.

Startup race scope: source inspection plus runtime startup checks establish no current eager UserService/JWKS dependency in consumers. Schema/app stacks are separate Compose projects: shared network does not order them. Setup already waits for infrastructure health then migrations; guide requires that phase complete before app startup. Simultaneous Docker Desktop group starts are not guaranteed safe for UserService's own database seeding. No blanket fix for DB/migration races claimed, no Compose ordering or application behavior changed.

**Next action / PAUSED:** user imports Refresh Probe as a separate collection, selects existing LOCAL environment, runs all four requests once with Keep variable values, shares only `sp_refresh_probe_report`. Do not ask for JWTs/profile PII, regenerate signing keys, or mark SPARK-327 complete. After diagnosis/resolution, the full restart/refresh/logout/expiry workflow and remaining accepted/deferred validation gaps still determine overall completion.

### 2026-11-06 — user probe passes; inspect actual failing import next

User screenshot shows Refresh Probe P0/P1/P2/P3 all HTTP 200 with 4/4 assertions passed, including both immediate header methods. The main imported collection reportedly still fails at 10. This rules out a universal inability to use freshly refreshed tokens and provides no evidence that an artificial delay is necessary. It does not establish the exact cause of the failing main request.

New user observation: Postman reports secret detected and moved to Local Vault while editing environment variables after a run. Official current Local Secret Protection documentation confirms automatic vault migration/reference replacement can occur when saving; behavior varies by plan/policy. This is now a concrete lead for persisted-reference versus run-local-token handling, not yet a confirmed root cause. Explain export steps and per-secret override/policy options; preserve Vault references for inspection, do not request secrets or recommend disabling protection as the fix. Shared testing guide updated with this lead and official links. Await actual main collection export/reference structure before further implementation.

Repository collection source still selects `sp_access_token` for step 10 and sets Authorization explicitly; the probe uses a run-local token directly from its response. No Postman connector capable of reading the user's imported collection is available in this session. Next required evidence is the **actual failing imported main collection JSON**, not another run of the same restart workflow or another speculative backend/header change. Ask for collection export only, excluding environments/run exports and removing any saved response examples or literal credentials/tokens. Inspect collection/folder/request scripts, auth inheritance, headers and variable definitions against repository artifact. PAUSED awaiting that export; no completion claim and no backend changes in this follow-up.

### 2026-11-06 — actual imported collection scripts inspected

User's menu exposes only Generate specification, so JSON export instructions did not match their view. User supplied `collection-pre-request.txt` and `collection-post-response.txt` from Downloads instead. Compared each with the repository collection event script after CRLF normalization and trimming: **both identical**. This rules out outdated shared scripts in the supplied evidence. No credentials were present in those script files.

Step 09 validates and saves the response access/refresh tokens; step 10 selects `sp_access_token`, removes Authorization and writes the selected bearer. An ordinary unresolved Vault reference read as the access token would stop in the pre-request token parsing guard rather than produce the reported HTTP 401. Vault involvement in a stale or overridden value remains unconfirmed. These two files do not contain request-level authorization, saved headers, request scripts or collection variable definitions; they cannot establish the final transmitted Authorization value.

Updated shared guide with the no-export fallback and exact expected request-10 configuration (No Auth; no saved Authorization header; empty request-level scripts; retain collection scripts). Asked user to report those actual settings without tokens. No runtime/script changes, service restarts or new test runs were warranted by identical scripts; do not repeat the full restart workflow or call the issue resolved. **PAUSED awaiting request-10 settings.**

### 2026-11-06 — request settings match; revision 2 response-token handoff

User confirms request 10 uses No Auth and empty request-level scripts; screenshot with generated headers visible confirms no saved Authorization row. This rules out those static overrides, not a runtime transmission discrepancy. Exact Desktop 401 cause remains OPEN; Vault involvement is not proven.

Updated main collection to **SmartPark SPARK-327 - JWT and JWKS checkpoints v2**, distinct ID, same canonical JSON path. Control initializes run-local access/refresh values from environment checkpoints. Login/refresh responses replace run-local values directly before persisting environment checkpoints. Current-session requests and identity/rotation assertions use the run-local values, matching the working probe's handoff. Deliberate original/tampered/expired/replayed-token tests retain their token selection. Final cleanup removes run-local session values. No backend validation/session behavior changed.

Added `sp_action=verify_refresh`: existing saved session and baseline → 00 → actual 09/10 → DIAGNOSTIC_ONLY, no restart/login/password requirement or claim of full completion. It rotates once and leaves the session available; full acceptance must subsequently use start_over. Added sanitized `sp_token_transport` (revision, source, environment/header equality flags, HTTP status), no token/claim/PII output. Header equality describes the request object, not independent wire capture. Unresolved/missing checkpoint inputs stop the diagnostic; no disabling Vault requirement or implicit Vault resolution support.

Validation:

- Node workflow/probe tests **14/14 PASS**, exit 0. New regression scenarios simulate stale/Vault-reference environment reads after refresh, verify current-run token and logout refresh selection while preserving old-token rejection, new-run initialization, no-restart diagnostic routing after access expiry, missing checkpoint guards and 401 failure reporting. These simulations do not establish that Postman actually rewrites environment values in this way.
- Actual Postman CLI against temporary synthetic loopback server: full restart-continuation segment through expiry pause **15 requests / 72 assertions PASS**, exit 0; new verify_refresh mode **3 requests / 23 assertions PASS**, exit 0. Fixture receiver confirms refreshed bearer on wire in both. Initial restricted connection failed EACCES; approved retry passed. Invented tokens only, no real account/database or running service changed, no cloud reporting/login.
- `git diff --check` PASS (existing line-ending warning only). Shared guide documents v2 import, exact diagnostic steps, safe result sharing and evidence limits; blank environment template includes the new report variable. Existing backend build/test evidence unchanged; no .NET source changes in this continuation.

**PAUSED for user:** import the v2 collection only, retain existing LOCAL environment, set `sp_action=verify_refresh`, run the whole v2 collection once with Keep variable values. Share `sp_status` and `sp_token_transport` (or failed step/assertion if 00/09 stops). No restart or key change. Keep SPARK-327 active until Desktop resolution and full remaining acceptance evidence.

### 2026-11-06 — final acceptance and closure

User first confirmed v2 request 10 HTTP 200 and DIAGNOSTIC_ONLY, then reported that all tests passed and explicitly requested task/test completion. Recorded this as user-reported acceptance; no final raw Runner export, exact assertion count or separately enabled-MFA result was supplied. The requested closure is not based solely on the earlier three-request diagnostic. Do not invent a final saved `sp_status` value or modify the user's environment to force COMPLETE.

Moved this record from active to completed and checked the completed implementation/verification objectives. Preserved historical failures/skips and separate follow-ups, including unrelated Facility fixtures and deferred consumer middleware. No source code, JWT keys, environment secrets, live services or databases changed for closure; no commit/push performed.

Added `docs/04-testing/reports/SPARK-327-postman-token-handoff-2026-11-06.md`: v1 environment-read versus v2 direct-response run-local handoff, evidence sequence, confirmed resolution versus unknown original runtime mechanism, prevention rules and acceptance scope. Updated the testing index and guide, removed obsolete header-only rerun instructions, and corrected the unsupported guarantee that a runtime-created diagnostic field must be visible in the saved environment. Missing `sp_token_transport` persistence remains unconfirmed and does not invalidate successful authentication checks.

Documentation verification: new report/guide/task relative links resolve and the task exists only in completed. A scan of 28 local links across touched documents also found two pre-existing unrelated missing README targets (`test-cases/demo-user-journey-2026-10-15.md` and `QUY-TRINH-BAM-TEST-DEMO.md`); these were not changed. `git diff --check` passed with the existing line-ending advisory. No automated runtime tests rerun for documentation-only closure; the 14/14 regression tests and controlled CLI results above remain the latest automated evidence.

### Completed follow-up — canonical Facility runner name

User requested replacing `-Service Parking` with `-Service Facility` in the application-runner examples. Updated QUICK_START app/host examples and UPDATE_SCRIPT_GUIDE accepted names. App start/stop maps now use Facility as the canonical key; Parking remains a backward-compatible alias, as Reservation remains an alias for Operation. Existing host-runner Facility support and legacy environment/database mapping keys were preserved. Both changed PowerShell scripts parsed without errors; targeted `git diff --check` passed. No containers were started/stopped.

Working-tree provenance changed since closure: the user staged prior work, and `compose.app.yml` is already unmerged (UU). Preserved those staged changes and the unrelated conflict; follow-up edits remain unstaged. Live Compose execution was not attempted. Task remains completed; the pre-existing Compose merge conflict is a separate workspace issue.
