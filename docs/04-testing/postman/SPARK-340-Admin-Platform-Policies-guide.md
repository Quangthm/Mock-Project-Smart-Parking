# SPARK-340 / SPARK-370 — P4 Manual & Automated Verification Guide

> **P1-R1 amendment — 25/11/2026:** PayOS-only Admin management is DEFERRED. `[NonController]` removes GET/PUT `/api/v1/admin/integrations/payment` from default MVC discovery. Normal requests return404 with an empty body/no Content-Type; permitted CORS preflight may204 and `X-Discovery-Service-Key` on this path causes existing middleware403. Auth/JWKS behavior is unchanged.
>
> FE displays **Payment configuration deferred** and **PayOS configuration is unavailable in this MVP.** No Payment form/load/save/status is active. Notification inspection/delivery/auth/audit, Policy APIs and all Payment runtime/settings/history/bindings/keys are retained. Original v1.6 approval remains historical.
>
> Inventory: **58 historical −10 deferred Payment positive/RBAC =48 retained +2 lock negatives =50 active requests**. DEFERRED is not PASS/SKIP. Historical results below are not verification of this revision. U19/SPARK-273, recipient correctness and legacy binding reconciliation remain open. The v1.6 enabled Payment-management scenarios below are historical and superseded for MVP by this amendment.
>
> This guide grants no runtime window. Docker/DDL/restart/operator steps below require a separately assigned target/window; do not run them during offline QA. Postman alone cannot observe no-history/no-provider effects: use isolated TestServer counters. Keep payment_test_* blank. Do not drop volumes, import bindings or use an app DB as a test fixture.


> F6–F8 update17/11: historical collection had58 requests, adding4 negative create/update cases for explicit platform arrivalLateToleranceEnabled true/false. Historical Newman54-request results remain historical, not a run of this revision. Before upgrading an existing Payment stack, follow [approved DDL/repair guide](../reports/SPARK-340-report.md#rollout); the dedicated binding table must exist before the new service binary runs. DDL/import have not been applied to the user's running DB by the implementation agent.

**Epic:** SPARK-340 — US-A02 "Admin: Configure platform policy bounds and authorized integration settings"
**Phase 4 task:** SPARK-370 — QA / Test Automation & Latent Defect Verification
**Artifacts covered by this guide**

| Artifact | Path |
|---|---|
| Postman collection | `docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json` |
| Postman environment (example) | `docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_environment.example.json` |
| Consolidated report / evidence / current tester prompt | [SPARK-340-report.md](../reports/SPARK-340-report.md#evidence) |
| Session log | `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md` (latest checkpoint) |

This guide is the step-by-step procedure to reproduce every P4 gate: bring up the stack, obtain tokens, run backend/frontend suites, run the Newman UAT collection, and complete the manual checklists for the four latent-defect clusters.

---

## 0. Prerequisites

- .NET 10 SDK, Node 24+, npm 11+, Docker 29+ with Compose v5+
- `npx` (Newman 6.x is invoked via `npx --no-install newman`; no global install required — verify with `npx --no-install newman --version`)
- Run all commands from the repository root:
  `D:\My file\FA26 - OJT FSoft\Mock Project OJT\Mock-Project-Smart-Parking`
- A local `.env` file (copy from `.env.example`). The compose files use `${VAR:?Set ...}` and will refuse to start if a required value is missing.

### Required environment variables

| Variable | Used by | Notes |
|---|---|---|
| `SMARTPARK_DEV_PASSWORD` | all Postgres + app | DB password, also app dev password |
| `SMARTPARK_RABBITMQ_PASSWORD` | RabbitMQ + app | message bus |
| `SMARTPARK_SERVICE_KEY` | app | internal service-to-service key |
| `SMARTPARK_ADMIN_PASSWORD` | app | seeded ADMIN password (see password policy below) |
| `SMARTPARK_OWNER_PASSWORD` | app | seeded BUSINESS_OWNER password |
| `SMARTPARK_DRIVER_PASSWORD` | test helper | your local seeded DRIVER password, used only to obtain the driver token |
| `SMARTPARK_OPERATOR_PASSWORD` | app | seeded SITE_OPERATOR password |
| `SMARTPARK_ADMIN_EMAIL` / `SMARTPARK_OWNER_EMAIL` / `SMARTPARK_OPERATOR_EMAIL` | app | optional overrides (defaults below) |

> **Seeded password policy:** 8–15 characters with at least one uppercase, one lowercase, one digit and one special character (enforced by `SeedRoleUserAsync`). Choose your own local value; do not commit it.

> **Secret hygiene:** never commit real tokens, api keys or checksum keys. Postman environment files kept in the repo must keep `admin_token`, `owner_token`, `driver_token`, `payment_test_*` blank or as placeholders.

---

## 1. Bring up the infrastructure (DB / Redis / RabbitMQ)

`compose.schema-integration.yml` creates the databases and the **`smartpark-schema_default`** network that `compose.app.yml` consumes as an external network.

```powershell
docker compose -f compose.schema-integration.yml up -d
```

What it starts:

| Service | Container DB | Host port |
|---|---|---|
| user-db | smartpark_user | 5441 |
| facility-db | smartpark_facility | 5442 |
| operations-db | smartpark_operations | 5443 |
| payment-db | smartpark_payment | 5444 |
| notification-db | smartpark_notification | 5445 |
| iot-db | smartpark_iot | 5446 |
| redis | — | 6379 |
| rabbitmq | — | 5672 (mgmt 15672) |

Verify:

```powershell
docker compose -f compose.schema-integration.yml ps
docker network ls | Select-String smartpark-schema_default
```

> The init SQL under `scripts/database/microservices/*.sql` is applied **only on an empty volume**. If a schema is stale, remove the relevant volume and re-create (this is destructive — do it only in a throwaway local environment).

---

## 2. Bring up the application

```powershell
docker compose -f compose.app.yml up -d --build
```

Services and ports:

| Service | URL |
|---|---|
| user-service | http://localhost:5035 |
| facility-service | http://localhost:5045 |
| operation-service | http://localhost:5055 |
| payment-service | http://localhost:5065 |
| notification-service | http://localhost:5068 |
| frontend | http://localhost:5173 |

Health checks:

```powershell
Invoke-RestMethod http://localhost:5035/health
Test-NetConnection 127.0.0.1 -Port 5055
Test-NetConnection 127.0.0.1 -Port 5065
Test-NetConnection 127.0.0.1 -Port 5068
```

All four must succeed before running Newman.

> If you cannot start the stack, skip to **§8 (stack-down path)** — the backend and frontend gates in §5–§6 do not require Docker.

---

## 3. Obtain tokens

Login endpoint: `POST {{user_base_url}}/api/auth/login` (rate limit 10/min per IP).
Body: `{ "email": "...", "password": "...", "turnstileToken": null }`
Response: `{ "success": true, "mfaRequired": false, "message": "Login successful", "data": { "accessToken": "...", ... } }`.

| Role | Email | Password | Purpose |
|---|---|---|---|
| ADMIN | `admin@smartpark.local` | `SMARTPARK_ADMIN_PASSWORD` | 200 on admin routes |
| BUSINESS_OWNER | `owner@smartpark.local` | `SMARTPARK_OWNER_PASSWORD` | 403 on admin routes |
| DRIVER | `driver@gmail.com` | `SMARTPARK_DRIVER_PASSWORD` | 403 on admin routes |

PowerShell helper:

```powershell
function Get-Token($email, $password) {
  $body = @{ email = $email; password = $password; turnstileToken = $null } | ConvertTo-Json
  (Invoke-RestMethod -Method Post -Uri "http://localhost:5035/api/auth/login" `
    -ContentType "application/json" -Body $body).data.accessToken
}

$adminToken   = Get-Token "admin@smartpark.local"   $env:SMARTPARK_ADMIN_PASSWORD
$ownerToken   = Get-Token "owner@smartpark.local"   $env:SMARTPARK_OWNER_PASSWORD
$driverToken  = Get-Token "driver@gmail.com"        $env:SMARTPARK_DRIVER_PASSWORD
```

Set `SMARTPARK_DRIVER_PASSWORD` in your shell to the locally seeded driver password; keep it out of the repository.

> If login is rejected because Turnstile is enforced in your environment, configure the local Turnstile test keys from `.env.example` (or the documented dev bypass) before retrying. The seeded accounts only exist when the service runs in Development.

---

## 4. Configure the Postman environment

1. Copy the example to a private file outside Git:
   ```powershell
   $spark340PrivateEnvironment = Join-Path $env:TEMP ('SPARK-340-env-' + [guid]::NewGuid().ToString('N') + '.json')
   Copy-Item docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_environment.example.json $spark340PrivateEnvironment
   ```
   Use this path in step7. Do **not** commit the populated environment.
2. Fill in `admin_token`, `owner_token`, `driver_token` from §3.
3. Confirm `tenant_id`, `site_id`, `vehicle_id`, `slot_id` point at an existing seeded tenant/site/vehicle/slot in your local DB. The collection captures revision ids, draft ids, `expectedUpdatedAt`, tenant/site echoes and scheduled timestamps from responses; it never hardcodes empty vehicle/slot ids.
4. Supply valid driver-owned vehicle/slot fixtures for the linked terms → receipt → booking flow. A separate existing booking or a revision read does not prove U19. If the commercial producer returns503, record FAIL/BLOCKED and downstream NOT RUN.
5. Historical Payment credential UAT is DEFERRED. Leave all payment_test_* variables blank; active MVP requests do not configure credentials or change mode.

Import both JSON files into Postman, or pass the env to Newman directly (§7).

---

## 5. Backend verification (no stack required)

```powershell
dotnet build SmartParking.slnx --nologo
dotnet test  SmartParking.slnx --nologo
```

Expected:

- Build: **0 errors**; record actual warnings and their causes.
- Test: **0 failed**; record actual pass/skip totals. Historical independent source review recorded1190 passed /114 skipped; these are historical evidence, not the expected output of every future checkout.

Targeted runs for the four clusters:

```powershell
# Cluster 1 — payment persistence + Postgres CHECK constraints
dotnet test tests/Services/SmartParking.PaymentService.Tests/SmartParking.PaymentService.Tests.csproj `
  --filter "FullyQualifiedName~PaymentIntegrationPersistenceAndCheckConstraintTests"

# Cluster 2 — webhook security + key rotation
dotnet test tests/Services/SmartParking.PaymentService.Tests/SmartParking.PaymentService.Tests.csproj `
  --filter "FullyQualifiedName~PayOsWebhookSecurityAndRotationTests"

# Cluster 3 — operation policy temporal / composed / concurrency
dotnet test tests/Services/SmartParking.OperationService.Tests/SmartParking.OperationService.Tests.csproj `
  --filter "FullyQualifiedName~PlatformPolic"

# Cluster 4 — uniform error envelope (code + errorCode)
dotnet test tests/Services/SmartParking.PaymentService.Tests/SmartParking.PaymentService.Tests.csproj `
  --filter "FullyQualifiedName~PaymentExceptionHandlerTests"
```

---

## 6. Frontend verification (no stack required)

```powershell
npm --prefix FE run build
node --test tests/frontend/*.test.cjs
```

Expected:

- Build: exit code **0**, `FE/dist/index.html` regenerated.
- Historical FE snapshot: **261 passed / 0 failed**. Run this checkout and report its actual totals; do not use261 as an expected result.

---

## 7. Run the Newman UAT collection (requires the stack)

```powershell
$spark340PrivateEnvironment = Read-Host 'Absolute private Postman environment path outside Git'
$spark340RawReport = Join-Path $env:TEMP ('SPARK-340-Newman-' + [guid]::NewGuid().ToString('N') + '.json')
npx --no-install newman run docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json `
  -e $spark340PrivateEnvironment `
  --reporters cli,json `
  --reporter-json-export $spark340RawReport
```

Raw JSON can contain authorization headers and private inputs. Keep it outside Git, sanitize it, then append actual run results/JSON to the [single report](../reports/SPARK-340-report.md#future-updates). Do not overwrite that Markdown file with the raw JSON exporter or recreate the retired per-run evidence files.

Useful variants:

- Run one folder only: `--folder "01 - Operation Policies Lifecycle"`
- Override a value without editing the env: `--env-var "site_id=<guid>"`
- Stop on first failure: `--bail`

### Per-folder scope (50 active requests; defined is not executed/passed)

| Folder | Requests | Key expectations |
|---|---|---|
| 00 - RBAC Matrix | 8 | Anonymous 401, Driver 403, Owner 403, Admin 200 for operations catalogue and notifications GET; Payment RBAC acceptance is DEFERRED |
| 01 - Operation Policies Lifecycle | 18 | Typed14-key catalogue with actual null/configured bounds; draft/history/pagination/by-id, valid optimistic update and stale-token409, activation/effective provenance. Four F8 negative writes reject site-only enablement true/false on both create and update with400 VALIDATION_FAILED and the correct field. |
| 02 - Payment Admin Deferred Lock | 2 | Admin GET/PUT require empty404 and absent Content-Type. PUT uses invalid empty payload to prevent a valid save if the lock regresses. TestServer counters, not Postman alone, prove no settings/history/provider effects. |
| 03 - Notification Integration | 1 | Exactly 4 channel enums `EMAIL`, `SMS`, `PUSH`, `IN_APP` mapped to `LOCAL_EMAIL_CAPTURE_FIXTURE`, `LOCAL_SMS_FIXTURE`, `LOCAL_PUSH_FIXTURE`, `IN_APP_FIXTURE`, all `status=ACTIVE`, `isReady=true` |
| 04 - Immutable Operational Policy Snapshot | 10 | Linked reviewed terms → durable receipt → same booking before/after activation, plus separate comparison of raw historical platform settings. Terms503 leaves the chain FAIL/BLOCKED and downstream NOT RUN. Source/UnitOfWork guards and policy revision reads do not replace accepted-booking snapshot evidence. U19 remains a SPARK-273 dependency. |
| 05 - Scheduling & Write RBAC | 11 | Future `effectiveFrom` activation returns `SCHEDULED` and the SCHEDULED listing contains it; a second draft activated at the same timestamp is rejected with 409 `OVERLAPPING_POLICY_INTERVAL`; Owner and Driver receive 403 on POST draft, PUT draft and activate |

### Skip handling

The collection reports prerequisite gaps with `pm.test.skip` instead of false passes:

- SITE provenance SKIPs when `tenant_id`/`site_id` are blank or the site has no active policy revision (`404 POLICY_NOT_FOUND`).
- The optional second SCHEDULED sanity check in folder 01 SKIPs when no scheduled revision exists yet; folder 05 creates one and proves the listing is non-empty.
- Historical SANDBOX credential management acceptance is DEFERRED; it is absent from the active collection, not counted as a skip/pass.
- Snapshot terms SKIPs when `vehicle_id`/`site_id`/`slot_id` are blank; booking re-reads SKIP when `reservation_id` is blank; the accepted-revision re-read SKIPs when no accepted revision was captured.

---

## 8. Stack-down path (no live Newman)

If Docker services cannot be started, the honest evidence path is:

1. Run §5 and §6 (they pass without a stack).
2. Parse the collection JSON and syntax-check every script:
   ```powershell
   node -e "const fs=require('fs');const c=JSON.parse(fs.readFileSync('docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json','utf8'));let r=0,e=[];function w(i){for(const x of i){if(x.item)w(x.item);else{r++;for(const s of (x.event||[]))if(s.script)try{new Function(s.script.exec.join('\n'))}catch(err){e.push(x.name+': '+err.message)}}}}w(c.item);console.log('folders',c.item.length,'requests',r);if(e.length){console.log(e.join('\n'));process.exit(1)}console.log('ALL SCRIPTS SYNTAX OK')"
   ```
   Expected for the current revision: `folders 6 requests 50`, then `ALL SCRIPTS SYNTAX OK`. Record actual script/environment counts from the checked-out files; don't copy historical51/54-request execution totals.
3. Do **not** overwrite the recorded SPARK-370 execution with a fabricated pass; record the stack-unavailable limitation in the session log and keep the existing sanitized evidence intact.

Do **not** record a Newman pass/fail that was not actually executed.

---

## 9. Manual verification checklists (four latent-defect clusters)

### Cluster 1 — Retained Payment persistence and unavailable Admin surface

- [ ] Admin GET/PUT receive empty404 in the normal pipeline; no configuration is read or changed through this surface.
- [ ] Offline settings/history/hydration/encryption/binding regressions remain in scope; report actual results.
- [ ] Previous Admin credential PUT/restart/GET durability acceptance is DEFERRED. It is not replaced by a fake success or counted PASS because the screen is locked.
- [ ] Runtime durability/DB/keys checks require a separately authorized target/window. Do not provision credentials or mutate shared storage through this guide's historical setup steps.

### Cluster 2 — Webhook security & order-key invariants

- [ ] On an isolated fixture, issue a checkout with an exact binding, rotate mode/key, and verify the issued bound key still authenticates the callback.
- [ ] Missing/corrupt binding is rejected with no current-key or timestamp inference; a trusted legacy mapping can be restored only through the approved repair flow. Retain bindings/key material with financial history, not an assumed blanket24h cutoff.
- [ ] Switch mode; a legitimate in-flight callback still validates against the key/mode bound at issuance.
- [ ] Trigger a provider error that echoes clientId/apiKey; response message and logs contain no plaintext credential (redacted).

### Cluster 3 — Operation policy temporal validity, composed invariants, concurrency

- [ ] Owner publish that crosses a future scheduled platform interval with narrower bounds → 409 `PolicyOutOfBoundsException` for the correct key.
- [ ] Admin activation of a platform revision whose composed effective policy violates `min ≤ max` for an intersecting site → 409 `PolicyBoundsConflictException` (`RESERVATION_MAX_DURATION`).
- [ ] Opposing owner-publish and admin-activation do not both succeed; the loser gets a conflict (GiST exclusion / compatibility check).
- [ ] A legacy site `DRIVER_REALLOCATION_REQUEST_LIMIT = 2` does not block a normal platform activation.

### Cluster 4 — P1-R1 scoped contract

- [ ] Payment Admin404 has an empty body/no Content-Type in the normal pipeline; do not expect its old JSON error codes.
- [ ] CORS preflight204 and discovery-service-header403 retain existing middleware behavior; keep authentication enabled.
- [ ] Active Notification/Policy contracts remain unchanged. Historical Payment positive/error-envelope HTTP acceptance is DEFERRED; direct handler/error-mapping unit coverage remains retained.

### Immutable snapshot

- [ ] Accept reviewed terms with valid fixtures, submit them, capture the durable receipt and link it to the resulting booking. Record accepted policy provenance and commercial amounts.
- [ ] Read that same linked booking before activation; capture its identity, state, amounts and accepted-policy evidence available through the supported verification path.
- [ ] Admin activates a changed platform revision.
- [ ] `GET .../effective` shows the replacement platform revision id and the changed `PAYMENT_HOLD_DURATION`.
- [ ] Re-read the same linked booking and verify the accepted snapshot is unchanged. Source/UnitOfWork tests support the persistence invariant but do not replace this end-to-end evidence. If the DTO omits the snapshot, document that evidence gap explicitly.
- [ ] Re-read the accepted platform revision by id: id, version, cancellation timezone and all 14 accepted policy values are unchanged (`effectiveUntil` may legitimately close when a later revision activates).
- [ ] If terms returns503 OWNER_UNAVAILABLE, keep U19 BLOCKED under SPARK-273; do not infer PASS from revision reads or unrelated bookings.

---

## 10. Evidence capture

- Save raw Newman JSON only to the private path outside Git chosen in step7. Append a sanitized snapshot and actual counts to [SPARK-340-report.md](../reports/SPARK-340-report.md#future-updates), not to a new dated report file.
- Optionally attach screenshots of Postman/Newman summaries (no secrets visible).
- Append a short checkpoint/link to the run's section in `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md`.

---

## 11. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `401` on admin routes | Missing/expired token, or `Authorization: Bearer {{token}}` not set |
| `403` with a valid token | Current Admin endpoints require lowercase `admin`; owner/driver are expected to get403. |
| `409` on `PUT draft` | Stale `If-Match`; re-read the revision and use its current `updatedAt` |
| `409` on activate | Overlapping interval (GiST) or an intersecting site policy conflict |
| `400 INVALID_PAYMENT_BASE_URL` | `baseUrl` not in the allowlist for the selected mode (`LIVE`/`SANDBOX` → `https://api-merchant.payos.vn`; `FIXTURE` → `http://localhost:5173` or `https://fixture.internal`) |
| `400 VALIDATION_FAILED` | Invalid mode, blank credential, or missing required draft ints |
| `ECONNREFUSED` in Newman | Stack not running; complete §1–§2 first |
| Seeded accounts missing | Service not in Development, or `SeedDemoUser`/`DevelopmentAdmin:Email` not configured |

---

## 12. Acceptance checklist

- [ ] `dotnet build` 0 errors
- [ ] `dotnet test` 0 failed; actual pass/fail/skip counts recorded
- [ ] `npm --prefix FE run build` exit 0
- [ ] `node --test tests/frontend/*.test.cjs`: record actual pass/fail/skip/total, not historical261
- [ ] Postman collection + environment valid (current collection:6 folders /50 requests; record actual variable count)
- [ ] Newman executed **or** limitation documented in evidence + session log
- [ ] Schema Freeze respected except the explicitly approved additive Payment binding-table exception; shared-target DDL/import requires its own authorized target/window
- [ ] No plaintext apiKey/checksumKey anywhere in artifacts/logs
- [ ] Reviewer does not commit/push unless explicitly assigned; no PR/merge/deployment in this verification task
