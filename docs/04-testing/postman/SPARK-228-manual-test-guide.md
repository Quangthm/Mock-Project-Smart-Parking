# SPARK-228 Operational Layout — Manual Test Guide

Manual end-to-end verification for the Phase 1 operational-layout read path. This is the runtime evidence for **B1** and supports **A1–A8**; exhaustive state/enum coverage still lives in the xUnit suites.

## Artifacts

| File | Purpose |
|---|---|
| `SmartParking-OperationalLayout-SPARK-228.postman_collection.json` | 33 requests in 9 folders, Postman Collection v2.1.0 |
| `SmartParking-OperationalLayout-SPARK-228.postman_environment.json` | 27 environment variables; real demo emails, empty password slots, no secrets |
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

The seed is idempotent and creates `DEMO-HQ` (`b0000000-0000-4000-8000-000000000001`), one floor, one zone, 48 slots (36 `STANDARD` + 12 `EV`) and the static capacity pools, and assigns the demo operator to the site with all five delegable permissions, including `SLOT_OVERRIDE`. There are **no motorcycle slots**: motorcycle capacity is a zone-level capacity value (`MOTORCYCLE`, `max_capacity` on the zone), so motorcycle bays are not individually managed.

## 2. Accounts

| Role | Account | Password source |
|---|---|---|
| Driver | `driver@gmail.com`, seeded by `DataSeeder.SeedAsync()` (user id `11111111-1111-1111-1111-111111111111`, role `DRIVER`, no tenant) | `Password@123`, already public in `DataSeeder.cs` |
| Owner (demo tenant) | `owner@smartpark.local` (`DevelopmentOwner:Email`) | `.env` key `SMARTPARK_OWNER_PASSWORD` (`SMARTPARK_OWNER_EMAIL` supplies the email) |
| Operator with `SLOT_OVERRIDE` | `operator@smartpark.local` (`DevelopmentOperator:Email`) | `.env` key `SMARTPARK_OPERATOR_PASSWORD` (`SMARTPARK_OPERATOR_EMAIL` supplies the email) |
| Admin | `admin@smartpark.local` (`DevelopmentAdmin:Email`) | `.env` key `SMARTPARK_ADMIN_PASSWORD` (`SMARTPARK_ADMIN_EMAIL` supplies the email) |
| Operator without `SLOT_OVERRIDE` | Invitation created by folder 00 request 0.7; activation is manual (open the invitation, call `POST /api/auth/bootstrap-password`), then set `operatorNoOverrideToken` | Chosen by the operator at activation; no generated/default value |
| Owner of another tenant (case 5.2) | None is seeded; create/approve one manually (§2.2) | Manual; leave `ownerOtherTenantEmail` empty to skip request 0.5 |
| Expired token (case 2.2) | None is seeded; obtain one manually (§2.3) | Manual; leave `expiredToken` empty to skip requests 0.9 and 2.2 |

Do not export Tokens/Passwords into the repository. The environment file ships with the real demo emails plus the public driver password `Password@123`; `ownerPassword`, `operatorPassword` and `adminPassword` are intentionally empty because the values live in the root `.env`.

### 2.1 Create an operator without SLOT_OVERRIDE (invitation flow)

Folder 00 does the creation: request 0.7 calls `POST /api/users` with the owner token. Since the develop merge / Fix2FA, operator onboarding is an **invitation** flow:

- `CreateOperatorDto` has **no password member** (`[JsonUnmappedMemberHandling(Disallow)]` rejects unknown properties such as `password` with 400). Send only `fullName`, `email`, `siteIds`, `permissions`.
- The call returns `201 {success:true, data:{id, fullName, email, role, status, createdBy, siteIds, permissions, deliveryId, deliveryStatus}}`. The account is not usable until the invitation is accepted.
- Activation is `POST /api/auth/bootstrap-password` with `{challengeId, token, password}`; `token` is the 64-hex value in the invitation email/SMS and `password` must be 8-15 chars with upper+lower+digit+special.
- **Postman cannot complete activation alone.** The invitation payload (challenge id + 64-hex token) is delivered out-of-band: `GET /api/auth/deliveries/{id}` returns only `{Id, Kind, Status, Attempts, SentAt}` (`AccountWorkflowService.DeliveryAsync:216`) and the protected payload is cleared after send. A human must open the delivered email/SMS and submit the bootstrap call.
- After activation, login with the chosen password and paste the access token into `operatorNoOverrideToken`; paste the chosen password into `operatorNoOverridePassword` only if you want request 0.8 to re-login.

Manual creation equivalent:

```http
POST {{userServiceUrl}}/api/users
Authorization: Bearer {{ownerToken}}
Content-Type: application/json
```

```json
{
  "fullName": "SPARK-228 Operator No Override",
  "email": "<operatorNoOverrideEmail>",
  "siteIds": ["{{siteId}}"],
  "permissions": ["DEVICE_STATUS_VIEW"]
}
```

- Success: `201` with `data.id`, `data.siteIds`, `data.permissions`, `data.deliveryId`, `data.deliveryStatus` (`pending`).
- Owner must hold `OPERATOR_MANAGE` (the `BUSINESS_OWNER` role has it; `AccountWorkflowService.RequirePermissionAsync`).
- Allowed permissions (`CreateOperatorDto.cs`): `DEVICE_MANAGE`, `DEVICE_STATUS_VIEW`, `CASH_COLLECT`, `APPEAL_REVIEW`, `SLOT_OVERRIDE`. Use `SLOT_OVERRIDE` for `operatorToken`; omit it for `operatorNoOverrideToken`.

Manual activation equivalent (values come from the delivered invitation):

```http
POST {{userServiceUrl}}/api/auth/bootstrap-password
Content-Type: application/json
```

```json
{
  "challengeId": "<challenge id from the invitation>",
  "token": "<64-hex token from the invitation>",
  "password": "<8-15 chars, upper+lower+digit+special>"
}
```

Then `POST {{userServiceUrl}}/api/auth/login` with that email/password and paste `data.accessToken` into `operatorNoOverrideToken`.

Folder 00 request order:

| # | Request | Purpose |
|---|---|---|
| 0.1 | Login Driver | Stores `driverToken`. |
| 0.2 | Login Owner (demo tenant) | Stores `ownerToken`. |
| 0.3 | Login Operator (with `SLOT_OVERRIDE`) | Stores `operatorToken`. |
| 0.4 | Login Admin | Stores `adminToken`. |
| 0.5 | Login Owner (other tenant, optional) | Stores `ownerOtherTenantToken`; logs SKIP when unset. |
| 0.6 | List owned sites and capture `siteId` | Stores `siteId` / `siteCode`. |
| 0.7 | Create Operator without `SLOT_OVERRIDE` (owner token) | Creates the invitation; stores `operatorNoOverrideEmail` / `operatorNoOverrideId`; no password. |
| 0.8 | Login Operator without `SLOT_OVERRIDE` | Re-login only when `operatorNoOverrideToken` is already set; otherwise logs SKIP. |
| 0.9 | Preflight: `expiredToken` is configured | Logs SKIP when `expiredToken` is empty. |

Verify the grant directly (after activation):

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

Case 5.2 intentionally targets the demo site that the second owner does **not** own. The service catches `SITE_NOT_FOUND` for a tenant-scoped owner and retries an unrestricted, Sanitized read, so **200 Sanitized is the expected result, not 403**. The created owner must belong to a **different tenant** and must **not** be assigned/site-scoped to the demo `{{siteId}}`; an owner of the demo tenant is not a valid substitute.

**Do not point `ownerOtherTenantEmail` at the primary owner** (`ownerEmail`, i.e. `owner@smartpark.local`). The primary owner genuinely owns `{{siteId}}`, so the request returns Telemetry visibility and seeing `BACKUP` is correct behaviour — the test data, not the API, is wrong. Request 5.2 detects this degenerate setup and logs:

```text
SKIP: ownerOtherTenantEmail is empty or equals ownerEmail - case 5.2 requires a distinct second-tenant owner account (see SPARK-228-manual-test-guide.md section 2.2). A genuine owner of this site is expected to see BACKUP and unredacted fields, so the Sanitized assertions are not evaluated.
```

The request then asserts nothing and does not fail, so the folder still completes. That is a **Blocked** result for case 5.2, not a pass: create/approve a distinct second-tenant owner (options 1–2 above), re-run folder 00 request 0.5, then re-run 5.2.

Request 0.5 logs `SKIP: ownerOtherTenantEmail is empty ...` and asserts nothing when `ownerOtherTenantEmail` / `ownerOtherTenantPassword` are unset, so the setup folder still completes.

### 2.3 Expired token (case 2.2)

`AccessTokenService.LifetimeSeconds = 86400` (24 hours), so a freshly issued token cannot be expired on demand.

- Login as driver, copy `data.accessToken`, wait until its `exp` passes (up to 24h), then paste it into `expiredToken`; **or**
- Ask a backend developer to mint a token with a past `exp` using the Development signing key.

Do not use a random string for case 2.2 (case 2.1 already covers malformed tokens), and do not use a logged-out/revoked token as an “expired” proof. If neither option is possible, mark 2.2 **Blocked**.

Request 0.9 and request 2.2 log `SKIP: expiredToken not configured ...` and assert nothing when `expiredToken` is empty, so they do not fail the run. Case 2.2 is then **Blocked**, not passed.

## 3. Configure and run in Postman

1. Import both JSON files.
2. Select **SmartParking - SPARK-228 Operational Layout (local)** as the active environment.
3. Fill the environment values locally (the environment already defaults to the real demo emails):
   - `driverEmail=driver@gmail.com` and `driverPassword=Password@123` are the shipped defaults.
   - `ownerPassword` ← `.env` `SMARTPARK_OWNER_PASSWORD`; `operatorPassword` ← `SMARTPARK_OPERATOR_PASSWORD`; `adminPassword` ← `SMARTPARK_ADMIN_PASSWORD`. If `.env` uses different `SMARTPARK_*_EMAIL` values, override `ownerEmail` / `operatorEmail` / `adminEmail` too.
   - `operatorNoOverrideEmail` is generated by request 0.7; `operatorNoOverrideId` is stored from its 201 response. Do not fill them.
   - `operatorNoOverrideToken` / `operatorNoOverridePassword`: leave empty unless a human activated the invited operator (§2.1); filling both enables 0.8, 4.3 and 8.3.
   - `ownerOtherTenantEmail` / `ownerOtherTenantPassword`: optional, only for case 5.2 (§2.2).
   - `expiredToken`: optional, only for cases 0.9 / 2.2 (§2.3).
   - `parkingServiceUrl` / `userServiceUrl` already default to `http://localhost:5045` / `http://localhost:5035`.
4. Run **00 - Setup** once. Confirm 0.1–0.4, 0.6 and 0.7 are green; 0.5, 0.8 and 0.9 log SKIP when their optional inputs are unset. This is mandatory: it stores all tokens, `siteId`, and `siteCode`.
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

### 3.3 Owner screen navigation (verified)

Log in as the owner and open **Parking Structure** directly from the owner sidebar (**Parking management → Parking Structure**). The **Sites** tab opens site management; entering the site's structure/operator-layout entry also lands on the same **Parking Structure** view (`OwnerDashboard` renders `ParkingStructure`; `FE/src/App.tsx` routes the owner view to `OwnerDashboard`).

### 3.4 F8: no write path from the 3D view

This is the **no write path from the 3D view** check:

1. Open DevTools → **Network** and clear it.
2. Sign in as the operator (with `SLOT_OVERRIDE`) and open the 3D lot view for the demo site.
3. Pan the view, rotate it, and select a bay.
4. Confirm the 3D viewer exposes **no create/update/delete control** for slots or layout (no button, context-menu item, or drag action that mutates).
5. Confirm the only layout request issued while panning/selecting is `GET /api/parking-lots/{id}/operational-layout`. The only other calls allowed are the identity/scope calls made when the session is established (login / `GET /api/auth/me`, `/api/operators/slot-scope`, `/api/auth/owner-scope`).
6. Any `POST`/`PUT`/`PATCH`/`DELETE` to a parking-lot or slot route is an A6 failure of the read path.

## 4. Reading a FAIL

Open the failed request → **Test Results** and read the assertion message. Common causes:

| Symptom | Likely cause / fix |
|---|---|
| 0.1–0.4 return 401 | Wrong `.env` password, email default not matching `.env`, or account locked after 3 bad passwords (15 min). The assertion message includes the first 200 chars of the response body. |
| 0.6 returns 401/403 | Owner login failed (fix 0.2), or the owner has no tenant membership / `OPERATOR_MANAGE`. Start UserService once, then run `./scripts/seed-demo-site.ps1` again. |
| 0.7 returns 400 | An old body still sends `password` (`CreateOperatorDto` has no such member and rejects unknown properties) or `siteId` was not captured (0.6 failed). Fix 0.6, then re-run 0.7. |
| 0.7 returns 409 | The generated invitation email collided; re-run folder 00. |
| 0.5 / 0.8 / 0.9 log SKIP | Expected when the optional second-tenant owner / activated no-override operator / `expiredToken` are unset; cases 5.2 / 4.3 / 8.3 / 2.2 stay **Blocked**, not passed. |
| 1.1/3.1 fail on `Cache-Control`/`Vary` | Wrong ParkingService build or a proxy stripping headers; check `Program.cs:153–154`. |
| 4.1 passes shape but logs “SKIP: no BACKUP” | Demo seed has no BACKUP. Use §3.2 to create one, or accept shape-only evidence. |
| 4.1 looks identical to 3.1 | Operator grant missing at this site. Re-run `seed-demo-site.ps1` or verify `/api/operators/slot-scope` returns 200. |
| 4.3 logs `SKIP: operatorNoOverrideToken is empty ...` | Expected until the operator invited by 0.7 is activated via the emailed invitation and its access token is pasted into `operatorNoOverrideToken` (§2.1). Case 4.3 is **Blocked**, not passed; zero assertions run. |
| 4.3 returns 403 or 401 after a token is set | Wrong account: `operatorNoOverrideToken` must be an **assigned** operator without `SLOT_OVERRIDE` and the token must be current. 8.3 is the route that intentionally returns 403. |
| 5.1/5.3 differ from operator telemetry | Owner/admin resolved to Sanitized. Check the tenant/site mapping and admin seed. |
| 5.2 logs `SKIP: ownerOtherTenantEmail is empty or equals ownerEmail ...` | Expected when no distinct second-tenant owner is configured (§2.2); case 5.2 stays **Blocked**. Using the primary owner is not a substitute. |
| 5.2 still shows BACKUP after a distinct owner is configured | The token is not actually from another tenant; use a distinct approved owner (§2.2). |
| 6.3 fails on `floorCode` | `floorCode` was not captured (1.1 failed) or the site has no slots. |
| 7.2 fails metric equality | Expected: Sanitized `reservedCount` is masked (genuine + BACKUP); the test asserts that relation, not literal equality. |
| 2.2 logs `SKIP: expiredToken not configured ...` | Expected when `expiredToken` is empty (§2.3); case 2.2 is **Blocked**, not passed; zero assertions run. |
| 8.3 logs `SKIP: operatorNoOverrideToken is empty ...` | Same blocker as 4.3; case 8.3 is **Blocked** until an activated no-override operator token is configured, then it must return 403 `FORBIDDEN`. |
| 8.4 returns 503 | ReservationService is not running; the legacy route reads a commitment snapshot. |

## 5. Case → acceptance map

Acceptance definitions follow the SPARK-228 matrix: A1 schema, A2 two-axis states, A3 bookable rule, A4 UNKNOWN fail-closed, A5 freshness/staleness, A6 read-only/no mutation, A7 redaction, A8 slot identity/mesh mapping; B1 is the runtime proof.

| Required case | Requests | Proves |
|---|---|---|
| 1 | 1.1 | A1 (envelope/version/threshold), A3 (bookable), A4 (UNKNOWN), A5 (freshness fields/headers), A7 (sanitized) |
| 2 | 1.2 | A1 (404/SITE_NOT_FOUND) |
| 3 | 1.3 | A6 (routing behavior); documents the expected empty 404 |
| 4 | 2.1 | A1/A6 (401 UNAUTHENTICATED, no data leak) |
| 5 | 2.2 | A1/A6 (expired session 401); SKIP/Blocked until `expiredToken` is set |
| 6 | 3.1 | A1, A3, A4, A7 (driver sanitized; no owner aggregate keys) |
| 7 | 4.1 | A2/A7 (telemetry BACKUP when present; differs from driver) |
| 8 | 4.2 | A2/A7 (telemetry metric relation; BACKUP not counted as RESERVED) |
| 9 | 4.3 | A7 (assigned operator without SLOT_OVERRIDE → 200 Sanitized, never 403); SKIP/Blocked until `operatorNoOverrideToken` is set |
| 10 | 5.1 | A2/A7 (owner telemetry matches operator) |
| 11 | 5.2 | A7 (other-tenant owner → 200 Sanitized, not 403) |
| 12 | 5.3 | A2/A7 (admin telemetry) |
| 13 | 6.1 | A1/A2 (CAR filter; metrics filter-independent) |
| 14 | 6.2 | A1 (400 INVALID_VEHICLE_TYPE) |
| 15 | 6.3, 6.4 | A1 (floor filter + 400 INVALID_FLOOR) |
| 16 | 7.1, 7.2 | A7 (two-token proof: same IDs/order, only redaction fields differ) |
| 17 | 8.1 | A6/A1 (legacy route 401) |
| 18 | 8.2 | A6/A7 (driver on legacy route 403) |
| 19 | 8.3 | A6/A7 (operator without SLOT_OVERRIDE on legacy route 403; contrast 4.3); SKIP/Blocked until `operatorNoOverrideToken` is set |
| 20 | 8.4 | A6 (legacy shape unchanged; not the new DTO) |
| 21 | 8.5, 8.6 | A6 (owner structure unchanged; anonymous 401) |
| B1 | Full run | End-to-end runtime evidence for the read path |

**Coverage limits, stated honestly:**

- **A2 (20/20 combinations):** this collection validates the two axes and redaction on live data, but the demo site does not seed all 20 combinations. Exhaustive 20/20 is proven by `OperationalLayoutContractFixtureTests` and `OperationalStateMappingTests`.
- **A5 (stale 30% + reconnect banner):** the API collection checks `generatedAt`, `isFresh`, and the 30s threshold only. The frontend banner/reconnect behavior is outside this API artifact.
- **A8 (`slotCode` ↔ GLB mesh 1:1):** 7.2 proves `slotCode` stability/order between roles. Exact mesh equality requires the GLB/ingestion fixture; the demo seed uses human codes (`CAR-01`), so A8 is only partially supported here.
- **Operator invitation (4.3 / 8.3):** creation is automated by 0.7, but the invitation token is delivered out-of-band and never exposed by the API, so activation requires a human. Until an activated `operatorNoOverrideToken` is configured, 4.3 and 8.3 log SKIP and are **Blocked**, not passed.

## 6. Source evidence used

| Topic | Evidence |
|---|---|
| Parking port `5045`, User port `5035` | `SmartParking.ParkingService.API/Properties/launchSettings.json`, `SmartParking.UserService.API/Properties/launchSettings.json`, `compose.app.yml` |
| Operational layout route, query validation, headers | `ParkingService.API/Program.cs:145–180` (`Cache-Control`/`Vary` at `:153–154`; `INVALID_VEHICLE_TYPE` at `:159–160`) |
| Legacy route / structure route | `Program.cs:203–209`, `:144` |
| Error → status mapping | `Program.cs:66–78` |
| Role resolution and 401/403 semantics | `Infrastructure/LayoutCallerResolver.cs:11–57`, `SlotOperatorClient.cs:8–28`, `ServiceClients.cs:10–40` |
| Login / me / role normalization | `UserService.API/Controllers/AuthController.cs:55–75`, `AccessTokenService.cs:24–35`, lifetime `:13` |
| SLOT_OVERRIDE grant | `UsersController.cs:31–37`, `CreateOperatorDto.cs`, `OperatorProvisioningService.cs:49–111`, `OperatorsController.cs:24–38`, `scripts/database/seeds/demo/01-identity.sql:57–80` |
| Operator invitation / activation | `CreateOperatorDto.cs` (no password member), `AccountWorkflowController.cs:31–33` (`bootstrap-password`), `AccountWorkflowService.DeliveryAsync:216` (delivery response omits the payload), `ChangeBootstrapPasswordDto` (`challengeId`, `token`, `password`) |
| Demo site/slots/operator | `scripts/seed-demo-site.ps1`, `scripts/database/seeds/demo/02-facility.sql` |
| Redaction behavior | `Application/OperationalLayoutRedaction.cs:11–85` |
| Legacy payload | `Application/OperatorLayoutService.cs:13–19` (raw `ParkingStructure`) |
| Masked-count computation before filtering | `Persistence/PostgresOperationalLayoutReader.cs:117–127`, filtering `:161–169` |

## 7. OPEN / not statically verifiable

- **Seed data:** the demo seed has 48 slots (36 `STANDARD` + 12 `EV`, no motorcycle slots) and no BACKUP; the exhaustive state fixture is not inserted into the running DB. Cases 4.1/5.1/5.3/7.2 therefore log a SKIP when no BACKUP exists.
- **A8 GLB equality:** no GLB asset is available to this collection; only slot identity/order is checked.
- **Other-tenant owner, activated no-override operator and expired token:** all need manual environment preparation; cases 5.2, 4.3, 8.3 and 2.2 are Blocked if the prerequisites are unavailable. In particular, the operator invitation token is never exposed by the API, so Postman cannot activate the account by itself.
- **A5 frontend behavior:** stale banner and reconnect are frontend concerns and are not exercised by these HTTP requests.
