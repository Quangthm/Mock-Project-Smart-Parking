# SPARK-228 Operational Layout — Manual Test Guide

Manual end-to-end verification for the Phase 1 operational-layout read path. This is the runtime evidence for **B1** and supports **A1–A8**; exhaustive state/enum coverage still lives in the xUnit suites.

## Artifacts

| File | Purpose |
|---|---|
| `SmartParking-OperationalLayout-SPARK-228.postman_collection.json` | 32 requests in 9 folders, Postman Collection v2.1.0 |
| `SmartParking-OperationalLayout-SPARK-228.postman_environment.json` | Environment variables; placeholders only, no secrets |
| `SPARK-228-manual-test-guide.md` | This run-book |

Collection schema URL: `https://schema.getpostman.com/json/collection/v2.1.0/collection.json`.

## 1. Prerequisites

- .NET SDK 10, Docker Desktop, PowerShell.
- Root `.env` created by `./setup.ps1` (never commit it).
- Schema containers running. Prefer `./setup.ps1`; the legacy `docker-compose.yml` is not the current stack.
- Start the three services in separate terminals from the repo root:

```powershell
./scripts/run-schema-service.ps1 -Service User        # http://localhost:5035
./scripts/run-schema-service.ps1 -Service Reservation # http://localhost:5055
./scripts/run-schema-service.ps1 -Service Parking     # http://localhost:5045
```

Ports are verified from `launchSettings.json` (User `5035`, Parking `5045`), `scripts/run-schema-service.ps1` (Reservation `5055`), and `compose.app.yml` host mappings.

- Seed the demo site **after UserService has started once** (so `DataSeeder` has created the demo owner/operator accounts):

```powershell
./scripts/seed-demo-site.ps1
```

The seed is idempotent and creates `DEMO-HQ` (`b0000000-0000-4000-8000-000000000001`), one floor, one zone, 8 slots, and assigns the demo operator to the site with all five delegable permissions, including `SLOT_OVERRIDE`.

## 2. Accounts

| Role | How to get it |
|---|---|
| Driver | Seeded by UserService: `driver@gmail.com` / `Password@123`. |
| Owner (matching tenant) | `SMARTPARK_OWNER_EMAIL` / `SMARTPARK_OWNER_PASSWORD` in `.env`; `seed-demo-site.ps1` attaches the owner account to the demo tenant. |
| Operator with `SLOT_OVERRIDE` | `SMARTPARK_OPERATOR_EMAIL` / `SMARTPARK_OPERATOR_PASSWORD`; `seed-demo-site.ps1` assigns the demo site and grants all permissions. |
| Admin | `SMARTPARK_ADMIN_EMAIL` / `SMARTPARK_ADMIN_PASSWORD` from `.env` (seeded at UserService startup). |
| Operator without `SLOT_OVERRIDE` | Create it as Owner (below). Use distinct `operatorNoOverrideEmail` / password variables. |
| Owner of another tenant | Register/verify/approve a second owner, or use a pre-provisioned QA owner (below). |
| Expired token | Obtain a genuinely expired access token (below); paste into `expiredToken`. |

Do not export Tokens/Passwords into the repository. The environment file ships with `driver@example.test` and `replace-with-...` placeholders only.

### 2.1 Create an operator without SLOT_OVERRIDE

Owner token, UserService (`UsersController.cs:31–37` → `OperatorProvisioningService.cs:49–94`):

```http
POST {{userServiceUrl}}/api/users
Authorization: Bearer {{ownerToken}}
Content-Type: application/json
```

```json
{
  "fullName": "SPARK-228 Operator No Override",
  "email": "<operatorNoOverrideEmail>",
  "password": "<8-15 chars, upper+lower+digit+special>",
  "siteIds": ["{{siteId}}"],
  "permissions": ["DEVICE_STATUS_VIEW"]
}
```

- Success: `201` with `data.id`, `data.siteIds`, `data.permissions`.
- The account is active immediately; login with the password you supplied. Email delivery is optional.
- Owner must hold `OPERATOR_MANAGE` (the `BUSINESS_OWNER` role has it; `AccountWorkflowService.RequirePermissionAsync`).
- Allowed permissions (`CreateOperatorDto.cs:18–19`): `DEVICE_MANAGE`, `DEVICE_STATUS_VIEW`, `CASH_COLLECT`, `APPEAL_REVIEW`, `SLOT_OVERRIDE`. Use `SLOT_OVERRIDE` for `operatorToken`; omit it for `operatorNoOverrideToken`.

Verify the grant directly:

```http
GET {{userServiceUrl}}/api/operators/slot-scope?siteId={{siteId}}
Authorization: Bearer {{operatorToken}}
# 200 {"userId":"...","tenantIds":["..."]}

GET {{userServiceUrl}}/api/operators/slot-scope?siteId={{siteId}}
Authorization: Bearer {{operatorNoOverrideToken}}
# 403
```

### 2.2 Owner of another tenant (case 5.2)

The demo seed creates only one tenant. Use one of:

1. **Existing QA owner in a second tenant** — set `ownerOtherTenantEmail` / password and login.
2. **Register and approve a second owner** with the existing `assigned-workflows-2026-10-08.postman_collection.json` folders **03 Owner registration** and **04 Admin approval** (register → verify email → admin `PATCH /api/owner-applications/{id}/review` `approved`). Then login and use that token.

Case 5.2 intentionally targets the demo site that the second owner does **not** own. The service catches `SITE_NOT_FOUND` for a tenant-scoped owner and retries an unrestricted, Sanitized read, so **200 Sanitized is the expected result, not 403**. If no second-tenant owner is available, mark 5.2 **Blocked** rather than substituting the same owner token.

### 2.3 Expired token (case 2.2)

`AccessTokenService.LifetimeSeconds = 86400` (24 hours), so a freshly issued token cannot be expired on demand.

- Login as driver, copy `data.accessToken`, wait until its `exp` passes (up to 24h), then paste it into `expiredToken`; **or**
- Ask a backend developer to mint a token with a past `exp` using the Development signing key.

Do not use a random string for case 2.2 (case 2.1 already covers malformed tokens), and do not use a logged-out/revoked token as an “expired” proof. If neither option is possible, mark 2.2 **Blocked**.

## 3. Configure and run in Postman

1. Import both JSON files.
2. Select **SmartParking - SPARK-228 Operational Layout (local)** as the active environment.
3. Fill the placeholders locally:
   - `driverEmail=driver@gmail.com`, `driverPassword=Password@123`.
   - `ownerEmail` / `ownerPassword`, `operatorEmail` / `operatorPassword`, `adminEmail` / `adminPassword` from `.env`.
   - `operatorNoOverrideEmail` / password, `ownerOtherTenantEmail` / password.
   - `expiredToken` from §2.3.
   - `parkingServiceUrl` / `userServiceUrl` already default to `http://localhost:5045` / `http://localhost:5035`.
4. Run **00 - Setup** once and confirm 0.1–0.7 are green. This is mandatory: it stores all tokens, `siteId`, and `siteCode`.
5. Run the full collection top-to-bottom. Folder order is intentional:
   - `00 Setup` → `01 Anonymous` → `02 Auth failures` → `03 Driver` → `04 Operator` → `05 Owner/Admin` → `06 Filters` → `07 A7 two-token` → `08 Legacy/regression`.
   - **7.1 must run before 7.2**: 7.1 saves `driverResponseBody`; 7.2 compares it with the operator response.
6. Keep “Stop run on error” off for the first diagnostic pass so cascaded prerequisites are visible.

### 3.1 Newman (optional, when available)

`npx` can fetch Newman, or use an installed `newman`:

```powershell
$s = & ./scripts/load-env.ps1 -EnvPath ./.env -NoExport

npx.cmd --yes --cache .cache/npm --package newman newman run `
  "docs/04-testing/postman/SmartParking-OperationalLayout-SPARK-228.postman_collection.json" `
  -e "docs/04-testing/postman/SmartParking-OperationalLayout-SPARK-228.postman_environment.json" `
  --env-var "driverEmail=driver@gmail.com" --env-var "driverPassword=Password@123" `
  --env-var "ownerEmail=$($s['SMARTPARK_OWNER_EMAIL'])" --env-var "ownerPassword=$($s['SMARTPARK_OWNER_PASSWORD'])" `
  --env-var "operatorEmail=$($s['SMARTPARK_OPERATOR_EMAIL'])" --env-var "operatorPassword=$($s['SMARTPARK_OPERATOR_PASSWORD'])" `
  --env-var "adminEmail=$($s['SMARTPARK_ADMIN_EMAIL'])" --env-var "adminPassword=$($s['SMARTPARK_ADMIN_PASSWORD'])" `
  --env-var "operatorNoOverrideEmail=$env:SPARK228_OPERATOR_NO_OVERRIDE_EMAIL" `
  --env-var "operatorNoOverridePassword=$env:SPARK228_OPERATOR_NO_OVERRIDE_PASSWORD" `
  --env-var "ownerOtherTenantEmail=$env:SPARK228_OWNER_OTHER_EMAIL" `
  --env-var "ownerOtherTenantPassword=$env:SPARK228_OWNER_OTHER_PASSWORD" `
  --env-var "expiredToken=$env:SPARK228_EXPIRED_TOKEN"
```

`load-env.ps1` returns a hashtable; adjust the keys if your local helper differs. Environment overrides are process-local and are not written to the repository.

### 3.2 Optional: make the BACKUP proof meaningful

The demo seed has **no BACKUP slots**, so telemetry-vs-sanitized differences are shape-only in cases 4.1 / 5.1 / 5.3 / 7.2. To strengthen A7 locally, mark one free demo slot BACKUP through the existing operator endpoint (requires `SLOT_OVERRIDE` and a free, operational, unoccupied slot):

```http
POST {{parkingServiceUrl}}/api/parking-lots/{{siteId}}/slots/<free-slotId>/backup
Authorization: Bearer {{operatorToken}}
Content-Type: application/json
```

```json
{"reason":"SPARK-228 manual A7 setup"}
```

Get `<free-slotId>` from `GET /api/parking-lots/{{siteId}}/structure` (`data.slots[].id`) or from the operational response. Then rerun 3.1, 4.1, 5.1 and 7.1–7.2. Telemetry then shows `BACKUP`; Sanitized masks it to `RESERVED` while the metric stays masked. This mutates only the local demo site; the collection itself remains GET-only.

## 4. Reading a FAIL

Open the failed request → **Test Results** and read the assertion message. Common causes:

| Symptom | Likely cause / fix |
|---|---|
| 0.1–0.6 return 401 | Placeholder credentials, wrong `.env` value, or account locked after 3 bad passwords (15 min). |
| 0.7 returns 403 | Owner has no tenant membership. Start UserService once, then run `./scripts/seed-demo-site.ps1` again. |
| 1.1/3.1 fail on `Cache-Control`/`Vary` | Wrong ParkingService build or a proxy stripping headers; check `Program.cs:153–154`. |
| 4.1 passes shape but logs “SKIP: no BACKUP” | Demo seed has no BACKUP. Use §3.2 to create one, or accept shape-only evidence. |
| 4.1 looks identical to 3.1 | Operator grant missing at this site. Re-run `seed-demo-site.ps1` or verify `/api/operators/slot-scope` returns 200. |
| 4.3 returns 403 | Wrong account: `operatorNoOverrideToken` must be an **assigned** operator without `SLOT_OVERRIDE`; 8.3 is the route that intentionally returns 403. |
| 5.1/5.3 differ from operator telemetry | Owner/admin resolved to Sanitized. Check the tenant/site mapping and admin seed. |
| 5.2 returns 401 | `ownerOtherTenantToken` is empty. |
| 5.2 shows BACKUP | The token is not actually from another tenant; use a distinct approved owner. |
| 6.3 fails on `floorCode` | `floorCode` was not captured (1.1 failed) or the site has no slots. |
| 7.2 fails metric equality | Expected: Sanitized `reservedCount` is masked (genuine + BACKUP); the test asserts that relation, not literal equality. |
| 8.4 returns 503 | ReservationService is not running; the legacy route reads a commitment snapshot. |

## 5. Case → acceptance map

Acceptance definitions follow the SPARK-228 matrix: A1 schema, A2 two-axis states, A3 bookable rule, A4 UNKNOWN fail-closed, A5 freshness/staleness, A6 read-only/no mutation, A7 redaction, A8 slot identity/mesh mapping; B1 is the runtime proof.

| Required case | Requests | Proves |
|---|---|---|
| 1 | 1.1 | A1 (envelope/version/threshold), A3 (bookable), A4 (UNKNOWN), A5 (freshness fields/headers), A7 (sanitized) |
| 2 | 1.2 | A1 (404/SITE_NOT_FOUND) |
| 3 | 1.3 | A6 (routing behavior); documents the expected empty 404 |
| 4 | 2.1 | A1/A6 (401 UNAUTHENTICATED, no data leak) |
| 5 | 2.2 | A1/A6 (expired session 401) |
| 6 | 3.1 | A1, A3, A4, A7 (driver sanitized; no owner aggregate keys) |
| 7 | 4.1 | A2/A7 (telemetry BACKUP when present; differs from driver) |
| 8 | 4.2 | A2/A7 (telemetry metric relation; BACKUP not counted as RESERVED) |
| 9 | 4.3 | A7 (assigned operator without SLOT_OVERRIDE → 200 Sanitized, never 403) |
| 10 | 5.1 | A2/A7 (owner telemetry matches operator) |
| 11 | 5.2 | A7 (other-tenant owner → 200 Sanitized, not 403) |
| 12 | 5.3 | A2/A7 (admin telemetry) |
| 13 | 6.1 | A1/A2 (CAR filter; metrics filter-independent) |
| 14 | 6.2 | A1 (400 INVALID_VEHICLE_TYPE) |
| 15 | 6.3, 6.4 | A1 (floor filter + 400 INVALID_FLOOR) |
| 16 | 7.1, 7.2 | A7 (two-token proof: same IDs/order, only redaction fields differ) |
| 17 | 8.1 | A6/A1 (legacy route 401) |
| 18 | 8.2 | A6/A7 (driver on legacy route 403) |
| 19 | 8.3 | A6/A7 (operator without SLOT_OVERRIDE on legacy route 403; contrast 4.3) |
| 20 | 8.4 | A6 (legacy shape unchanged; not the new DTO) |
| 21 | 8.5, 8.6 | A6 (owner structure unchanged; anonymous 401) |
| B1 | Full run | End-to-end runtime evidence for the read path |

**Coverage limits, stated honestly:**

- **A2 (20/20 combinations):** this collection validates the two axes and redaction on live data, but the demo site does not seed all 20 combinations. Exhaustive 20/20 is proven by `OperationalLayoutContractFixtureTests` and `OperationalStateMappingTests`.
- **A5 (stale 30% + reconnect banner):** the API collection checks `generatedAt`, `isFresh`, and the 30s threshold only. The frontend banner/reconnect behavior is outside this API artifact.
- **A8 (`slotCode` ↔ GLB mesh 1:1):** 7.2 proves `slotCode` stability/order between roles. Exact mesh equality requires the GLB/ingestion fixture; the demo seed uses human codes (`CAR-01`), so A8 is only partially supported here.

## 6. Source evidence used

| Topic | Evidence |
|---|---|
| Parking port `5045`, User port `5035` | `SmartParking.ParkingService.API/Properties/launchSettings.json`, `SmartParking.UserService.API/Properties/launchSettings.json`, `compose.app.yml` |
| Operational layout route, query validation, headers | `ParkingService.API/Program.cs:145–180` (`Cache-Control`/`Vary` at `:153–154`; `INVALID_VEHICLE_TYPE` at `:159–160`) |
| Legacy route / structure route | `Program.cs:203–209`, `:144` |
| Error → status mapping | `Program.cs:66–78` |
| Role resolution and 401/403 semantics | `Infrastructure/LayoutCallerResolver.cs:11–57`, `SlotOperatorClient.cs:8–28`, `ServiceClients.cs:10–40` |
| Login / me / role normalization | `UserService.API/Controllers/AuthController.cs:55–75`, `AccessTokenService.cs:24–35`, lifetime `:13` |
| SLOT_OVERRIDE grant | `UsersController.cs:31–37`, `CreateOperatorDto.cs:18–19`, `OperatorProvisioningService.cs:49–111`, `OperatorsController.cs:24–38`, `scripts/database/seeds/demo/01-identity.sql:57–80` |
| Demo site/slots/operator | `scripts/seed-demo-site.ps1`, `scripts/database/seeds/demo/02-facility.sql` |
| Redaction behavior | `Application/OperationalLayoutRedaction.cs:11–85` |
| Legacy payload | `Application/OperatorLayoutService.cs:13–19` (raw `ParkingStructure`) |
| Masked-count computation before filtering | `Persistence/PostgresOperationalLayoutReader.cs:117–127`, filtering `:161–169` |

## 7. OPEN / not statically verifiable

- **Seed data:** the demo seed has 8 slots and no BACKUP; the 48-slot exhaustive fixture is not inserted into the running DB. Cases 4.1/5.1/5.3/7.2 therefore log a SKIP when no BACKUP exists.
- **A8 GLB equality:** no GLB asset is available to this collection; only slot identity/order is checked.
- **Other-tenant owner and expired token:** both need manual environment preparation; cases 5.2 and 2.2 are Blocked if the prerequisites are unavailable.
- **A5 frontend behavior:** stale banner and reconnect are frontend concerns and are not exercised by these HTTP requests.
