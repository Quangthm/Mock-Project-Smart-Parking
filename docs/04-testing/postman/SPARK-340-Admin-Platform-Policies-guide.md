# SPARK-340 / SPARK-370 — P4 Manual & Automated Verification Guide

**Epic:** SPARK-340 — US-A02 "Admin: Configure platform policy bounds and authorized integration settings"
**Phase 4 task:** SPARK-370 — QA / Test Automation & Latent Defect Verification
**Artifacts covered by this guide**

| Artifact | Path |
|---|---|
| Postman collection | `docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json` |
| Postman environment (example) | `docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_environment.example.json` |
| Newman / UAT evidence | `docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json` |
| Session log | `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md` (Section 19) |

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

1. Copy the example to a local, uncommitted file:
   ```powershell
   Copy-Item docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_environment.example.json `
             docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.local.postman_environment.json
   ```
   (Do **not** commit the `.local.` copy.)
2. Fill in `admin_token`, `owner_token`, `driver_token` from §3.
3. Confirm `tenant_id`, `site_id`, `vehicle_id`, `slot_id` point at an existing seeded tenant/site/vehicle/slot in your local DB. The collection captures revision ids, draft ids, `expectedUpdatedAt`, tenant/site echoes and scheduled timestamps from responses; it never hardcodes empty vehicle/slot ids.
4. Optionally set `reservation_id` to an existing confirmed booking owned by the same driver as `driver_token`. The snapshot folder uses it to read the booking before and after activation; when it is blank those two requests SKIP with an explicit message.
5. Leave `payment_test_client_id`, `payment_test_api_key`, `payment_test_checksum_key` blank unless you are exercising the optional SANDBOX-credential request. When any of them is blank the request SKIPs; when supplied, the collection requires `200`, `apiKeyConfigured=true`, and no plaintext echo (a `400` never passes).

Import both JSON files into Postman, or pass the env to Newman directly (§7).

---

## 5. Backend verification (no stack required)

```powershell
dotnet build SmartParking.slnx --nologo
dotnet test  SmartParking.slnx --nologo
```

Expected:

- Build: **0 errors** (only pre-existing NU1902 / NU1903 NuGet advisories).
- Test: **1,126 passed / 102 skipped / 0 failed**.

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
- Tests: **261 passed / 0 failed**.

---

## 7. Run the Newman UAT collection (requires the stack)

```powershell
npx --no-install newman run docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json `
  -e docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.local.postman_environment.json `
  --reporters cli,json `
  --reporter-json-export docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json
```

Useful variants:

- Run one folder only: `--folder "01 - Operation Policies Lifecycle"`
- Override a value without editing the env: `--env-var "site_id=<guid>"`
- Stop on first failure: `--bail`

### Per-folder expected outcome (51 requests total)

| Folder | Requests | Key expectations |
|---|---|---|
| 00 - RBAC Matrix | 12 | Anonymous 401, Driver 403, Owner 403, Admin 200 for operations catalogue, payment GET, notifications GET |
| 01 - Operation Policies Lifecycle | 14 | Catalogue exposes exactly the 14 canonical keys (`ARRIVAL_LATE_TOLERANCE` has the 15..30 bound; all other keys null; four anti-abuse keys not overridable; ten time/count keys overridable); isolated draft create 201; `status=DRAFT` contains it; pagination `page=1 pageSize=5`; by-id 200 and 404; `PUT` with current `If-Match` 200 and an advanced token; stale `If-Match` 409 `RESOURCE_VERSION_CONFLICT`; immediate activate 200; CURRENT contains the activated revision and is non-empty; SUPERSEDED contains the closed predecessor; PLATFORM effective reports `inherited=false` / `source=PLATFORM_DEFAULT` and `arrivalLateTolerance.enabled=null`; SITE effective asserts inherited keys `PLATFORM_DEFAULT`, overridden keys `SITE_OVERRIDE`, and SKIPs when no active site policy exists |
| 02 - Payment Integration | 6 | GET returns `apiKeyConfigured`/`checksumKeyConfigured` booleans, no raw `apiKey`/`checksumKey`, masked `clientIdMasked`, capabilities; FIXTURE switch 200 `HEALTHY`; disallowed BaseUrl 400 `INVALID_PAYMENT_BASE_URL`; invalid mode 400 `VALIDATION_FAILED`; blank credential 400; SANDBOX credentials SKIP when `payment_test_*` is blank, otherwise require 200 + configured flags + no plaintext echo |
| 03 - Notification Integration | 1 | Exactly 4 channel enums `EMAIL`, `SMS`, `PUSH`, `IN_APP` mapped to `LOCAL_EMAIL_CAPTURE_FIXTURE`, `LOCAL_SMS_FIXTURE`, `LOCAL_PUSH_FIXTURE`, `IN_APP_FIXTURE`, all `status=ACTIVE`, `isReady=true` |
| 04 - Immutable Operational Policy Snapshot | 7 | Accept terms captures `operationalPolicy.platformRevisionId`, platform version, `PAYMENT_HOLD_DURATION` and commercial amounts; optional booking read before activation captures observable persisted fields; isolated changed draft is activated; PLATFORM effective moves to the replacement revision and shows the changed value; optional booking read after activation proves id/state/amounts/timestamps unchanged (the snapshot column is not exposed by `BookingView`; the `UnitOfWork` guard and xUnit are authoritative); accepted platform revision is re-read and its identity/version/policy value are unchanged |
| 05 - Scheduling & Write RBAC | 11 | Future `effectiveFrom` activation returns `SCHEDULED` and the SCHEDULED listing contains it; a second draft activated at the same timestamp is rejected with 409 `OVERLAPPING_POLICY_INTERVAL`; Owner and Driver receive 403 on POST draft, PUT draft and activate |

### Skip handling

The collection reports prerequisite gaps with `pm.test.skip` instead of false passes:

- SITE provenance SKIPs when `tenant_id`/`site_id` are blank or the site has no active policy revision (`404 POLICY_NOT_FOUND`).
- The optional second SCHEDULED sanity check in folder 01 SKIPs when no scheduled revision exists yet; folder 05 creates one and proves the listing is non-empty.
- SANDBOX credential proof SKIPs when any `payment_test_*` variable is blank.
- Snapshot terms SKIPs when `vehicle_id`/`site_id`/`slot_id` are blank; booking re-reads SKIP when `reservation_id` is blank; the accepted-revision re-read SKIPs when no accepted revision was captured.

---

## 8. Stack-down path (no live Newman)

If Docker services cannot be started, the honest evidence path is:

1. Run §5 and §6 (they pass without a stack).
2. Parse the collection JSON and syntax-check every script:
   ```powershell
   node -e "const fs=require('fs');const c=JSON.parse(fs.readFileSync('docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json','utf8'));let r=0,e=[];function w(i){for(const x of i){if(x.item)w(x.item);else{r++;for(const s of (x.event||[]))if(s.script)try{new Function(s.script.exec.join('\n'))}catch(err){e.push(x.name+': '+err.message)}}}}w(c.item);console.log('folders',c.item.length,'requests',r);if(e.length){console.log(e.join('\n'));process.exit(1)}console.log('ALL SCRIPTS SYNTAX OK')"
   ```
   Expected: `folders 6 requests 51`, then `ALL SCRIPTS SYNTAX OK`. The collection has 6 folders / 51 requests; the example environment still declares 22 variables.
3. Do **not** overwrite the recorded SPARK-370 execution with a fabricated pass; record the stack-unavailable limitation in the session log and keep the existing sanitized evidence intact.

Do **not** record a Newman pass/fail that was not actually executed.

---

## 9. Manual verification checklists (four latent-defect clusters)

### Cluster 1 — Payment persistence & Postgres schema compatibility

- [ ] `PUT /api/v1/admin/integrations/payment` with SANDBOX credentials returns 200 and `apiKeyConfigured=true`.
- [ ] Restart `payment-service`; `GET` the same endpoint still returns the saved mode/baseUrl/credentials (hydration round-trip).
- [ ] In `payment-db`, confirm the appended revision conforms to the CHECK constraints:
  `policy_type = 'TARIFF'`, `scope_type = 'GLOBAL'` (not `PAYMENT_INTEGRATION` / `PLATFORM`).
- [ ] Confirm no plaintext credential appears in `policy_values_json` (Data Protection encrypted) and none is echoed by the API.

### Cluster 2 — Webhook security & order-key invariants

- [ ] Create an order under SANDBOX; rotate the checksum key; a callback signed with the previous key **within** the 24h grace window validates.
- [ ] A callback for an order created **before** the key's retention window (e.g. 48h ago) is rejected (`IsValidSignature=false`).
- [ ] Switch mode; a legitimate in-flight callback still validates against the key/mode bound at issuance.
- [ ] Trigger a provider error that echoes clientId/apiKey; response message and logs contain no plaintext credential (redacted).

### Cluster 3 — Operation policy temporal validity, composed invariants, concurrency

- [ ] Owner publish that crosses a future scheduled platform interval with narrower bounds → 409 `PolicyOutOfBoundsException` for the correct key.
- [ ] Admin activation of a platform revision whose composed effective policy violates `min ≤ max` for an intersecting site → 409 `PolicyBoundsConflictException` (`RESERVATION_MAX_DURATION`).
- [ ] Opposing owner-publish and admin-activation do not both succeed; the loser gets a conflict (GiST exclusion / compatibility check).
- [ ] A legacy site `DRIVER_REALLOCATION_REQUEST_LIMIT = 2` does not block a normal platform activation.

### Cluster 4 — Contract unification

- [ ] Every admin payment error envelope contains **both** `code` and `errorCode` with the same value, plus `success=false`, `message`, `traceId`, `errors`, `timestamp`.
- [ ] `INVALID_PAYMENT_BASE_URL` is present as both `code` and `errorCode` on the disallowed-BaseUrl case.

### Immutable snapshot

- [ ] Accept reservation terms; capture `operationalPolicy.platformRevisionId`, platform version, `PAYMENT_HOLD_DURATION` and the commercial amounts from the response.
- [ ] (Optional) Set `reservation_id` to a confirmed booking and read `GET /api/me/reservations/{id}` before activation; capture `reservationId`, `state`, `start`, `end`, `bookedAmount`, `depositAmount`, `currency`, `createdAt`.
- [ ] Admin activates a changed platform revision.
- [ ] `GET .../effective` shows the replacement platform revision id and the changed `PAYMENT_HOLD_DURATION`.
- [ ] (Optional) Re-read the same booking: id/state/amounts/timestamps are unchanged. The booking DTO does not expose `acceptedOperationalPolicySnapshot`; the DB `UnitOfWork` guard plus OperationService xUnit tests are authoritative for the snapshot column.
- [ ] Re-read the accepted platform revision by id: id, version, cancellation timezone and all 14 accepted policy values are unchanged (`effectiveUntil` may legitimately close when a later revision activates).

---

## 10. Evidence capture

- Save the Newman JSON export to `docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json`.
- Optionally attach screenshots of Postman/Newman summaries (no secrets visible).
- Append the run result (timestamp, pass/fail counts, environment) to `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md` Section 19.

---

## 11. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `401` on admin routes | Missing/expired token, or `Authorization: Bearer {{token}}` not set |
| `403` with a valid token | Token role is not `ADMIN` (owner/driver are expected to get 403) |
| `409` on `PUT draft` | Stale `If-Match`; re-read the revision and use its current `updatedAt` |
| `409` on activate | Overlapping interval (GiST) or an intersecting site policy conflict |
| `400 INVALID_PAYMENT_BASE_URL` | `baseUrl` not in the allowlist for the selected mode (`LIVE`/`SANDBOX` → `https://api-merchant.payos.vn`; `FIXTURE` → `http://localhost:5173` or `https://fixture.internal`) |
| `400 VALIDATION_FAILED` | Invalid mode, blank credential, or missing required draft ints |
| `ECONNREFUSED` in Newman | Stack not running; complete §1–§2 first |
| Seeded accounts missing | Service not in Development, or `SeedDemoUser`/`DevelopmentAdmin:Email` not configured |

---

## 12. Acceptance checklist

- [ ] `dotnet build` 0 errors
- [ ] `dotnet test` 0 failed (≥ 1,126 passed)
- [ ] `npm --prefix FE run build` exit 0
- [ ] `node --test tests/frontend/*.test.cjs` 261/261
- [ ] Postman collection + environment valid (6 folders / 51 requests / 22 vars)
- [ ] Newman executed **or** limitation documented in evidence + session log
- [ ] Schema Freeze respected (no migrations/DDL)
- [ ] No plaintext apiKey/checksumKey anywhere in artifacts/logs
- [ ] Working tree uncommitted (no commit/push)