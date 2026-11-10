# SPARK-238 Facility Structure — Manual Test Guide

Manual end-to-end verification for the SPARK-238 Owner-facing structure configuration surface. This run-book is the human companion to the Postman artifacts in this folder. It covers every FacilityService route the task enumerates: lots, units, slots, paths, the `PATCH /structure` action family, reads, maintenance windows, the same-lot batch, operations, and the negative/auth boundaries.

The route paths, request records and response fields used by the collection come from `src/Services/FacilityService/SmartParking.FacilityService.API/Program.cs` (the source of truth), not from a summary. Where API-design v1.3 disagrees with the code, the code is followed and the difference is listed in **section 5**.

## 1. Artifacts

| File | Purpose |
|---|---|
| `SmartParking-FacilityStructure-SPARK-238.postman_collection.json` | 83 requests in 11 folders, Postman Collection v2.1.0 |
| `SmartParking-FacilityStructure-SPARK-238.postman_environment.json` | 62 environment variables; real demo emails, empty secret slots, no committed secrets |
| `SPARK-238-manual-test-guide.md` | This run-book |

Collection schema URL: `https://schema.getpostman.com/json/collection/v2.1.0/collection.json`.

Folder layout:

| Folder | Requests | Concern |
|---|---|---|
| `00 - Setup (run first)` | 4 | Login owner / operator / driver / admin; generate `runSuffix` |
| `01 - Lot` | 6 | `POST`/`GET /api/parking-lots`, inactive-lot listing, rejected legacy ingest |
| `02 - Units` | 6 | FLOOR → ZONE → BLOCK, duplicate sibling, hierarchy order |
| `03 - Slots` | 11 | single / multi vehicle types, live duplicate, released code, restore |
| `04 - Paths` | 2 | `POST /{id}/paths` |
| `05 - Structure PATCH actions` | 18 | every action + the `configureSlot` geometry rejection |
| `06 - Reads` | 4 | structure, operational-layout, operator-layout |
| `07 - Maintenance windows` | 11 | list / create / overlap / extend / cancel |
| `08 - Batch` | 7 | missing header 400, success, replay, per-item failures, all-or-nothing |
| `09 - Operations` | 4 | `POST /operations`, `GET /operations/{id}`, replay, unknown id |
| `10 - Negative / Auth` | 10 | 401 / 403 / 404 boundaries |

## 2. Prerequisites

- .NET SDK 10, Docker Desktop, PowerShell.
- Root `.env` created by `./setup.ps1` (never commit it).
- Schema containers running. Prefer `./setup.ps1`; the legacy `docker-compose.yml` is not the current stack.
- Start the three services in separate terminals from the repo root:

```powershell
./scripts/run-schema-service.ps1 -Service User        # http://localhost:5035
./scripts/run-schema-service.ps1 -Service Reservation # http://localhost:5055
./scripts/run-schema-service.ps1 -Service Facility    # http://localhost:5045
```

Ports: User `5035`, Reservation/Operation `5055`, Facility `5045`. See `SPARK-228-manual-test-guide.md` for the same evidence.

- Seed the demo site **after UserService has started once** (so the seeded owner/operator accounts exist):

```powershell
./scripts/seed-demo-site.ps1
```

The seed creates `DEMO-HQ` (`b0000000-0000-4000-8000-000000000001`) and assigns the demo operator to it with `SLOT_OVERRIDE`. `seededSiteId` and `seededFloorId` in the environment name those rows.

- All structure mutations and `GET /structure` require **ReservationService**: the Facility middleware takes a commitment hold/snapshot through `IStructureCommitments`. If Reservation is down, reads and writes fail with `503 COMMITMENTS_UNAVAILABLE` before anything is changed locally. `POST /operations` additionally calls the Reservation impact endpoint via `StructureImpactPlanner`.

## 3. Accounts

| Role | Account | Password source | Used by |
|---|---|---|---|
| Owner | `owner@smartpark.local` (`SMARTPARK_OWNER_EMAIL`) | the password you chose at Owner registration. The `SMARTPARK_OWNER_PASSWORD` value currently in `.env` returns `AUTH_FAILED`, so use the real one | Every structure route |
| Operator with `SLOT_OVERRIDE` | `operator@smartpark.local` (`SMARTPARK_OPERATOR_EMAIL`) | `.env` `SMARTPARK_OPERATOR_PASSWORD` | `GET /{id}/operator-layout` |
| Driver | `driver@gmail.com`, seeded | `Password@123` (public in `DataSeeder`) | 10.10 driver-token 403 |
| Admin | `admin@smartpark.local` (`SMARTPARK_ADMIN_EMAIL`) | `.env` `SMARTPARK_ADMIN_PASSWORD` | Optional; not required |

The demo Owner holds `BUSINESS_OWNER`, so `owner-scope` grants `PARKING_MANAGE` and `BACKUP_CONFIGURE`; that is why 5.15 (`backup`) is expected to return 200.

`BACKUP_MARK` is **not** granted by default. The owner-scope query is `(a.permissions IS NULL AND @permission <> 'BACKUP_MARK') OR @permission = ANY(a.permissions)` (`IdentityWorkflowStore` / `IdentityAccessRepository`), and the demo Owner's `accounts.permissions` is NULL — so `BACKUP_MARK` is denied unless it is granted explicitly, per DEC-LOT-04. 5.16/5.17 (`markBackup`) therefore expect **403 `FORBIDDEN`**, which is exactly what the collection asserts. Do not "fix" these two cases to 200 without first granting `BACKUP_MARK` to the Owner account.

Do not export tokens or passwords into the repository. The environment ships the real demo emails plus the public driver password; `ownerPassword`, `operatorPassword` and `adminPassword` are intentionally empty.

## 4. Configure and run

1. Import the collection and the environment JSON.
2. Select **SmartParking - SPARK-238 Facility Structure (local)** as the active environment.
3. Fill the environment locally:
   - `baseUrl=http://localhost:5045`, `userServiceUrl=http://localhost:5035`, `reservationServiceUrl=http://localhost:5055` are shipped defaults.
   - `ownerPassword` ← `.env` `SMARTPARK_OWNER_PASSWORD`; `operatorPassword` ← `SMARTPARK_OPERATOR_PASSWORD`; `adminPassword` ← `SMARTPARK_ADMIN_PASSWORD`. Override `ownerEmail` / `operatorEmail` / `adminEmail` if `.env` uses different `SMARTPARK_*_EMAIL` values.
   - `runSuffix`, `createdSiteId`, all unit/slot/path ids, window ids, operation ids and keys are generated or captured at run time. Leave them empty.
4. Run **00 - Setup** once. It must be green before anything else: it stores all four tokens and `runSuffix`.
5. Run the full collection top-to-bottom. Folder order is intentional (units before slots, slots before the batch). Keep **Stop run on error** off for a first diagnostic pass so cascaded prerequisites are visible.

### 4.1 Data prerequisites per case

| Case | Needs |
|---|---|
| 6.4 `operator-layout` | `scripts/seed-demo-site.ps1` (operator assigned to `seededSiteId`) + ReservationService |
| 8.4 cross-lot batch | nothing beyond the seed value in `seededFloorId`; any id not in `createdSiteId` yields `NOT_FOUND` |
| 5.15 `backup` | Owner `BACKUP_CONFIGURE` (granted) → expects 200 |
| 5.16 / 5.17 `markBackup` | Owner `BACKUP_MARK` — **not granted** on the demo Owner (DEC-LOT-04) → expects 403 |
| 7.x windows | nothing; every timestamp is generated relative to `now` |
| 6.2 / 6.3 | ReservationService is **not** needed (the operational read path takes no hold) |

### 4.2 Resetting between runs

The collection mutates data on purpose. Re-running is safe: `runSuffix` (request 0.1) makes the new lot codes unique, so each run creates a fresh lot with its own units/slots/paths. The demo site itself is never mutated by this collection except by `GET /operator-layout`, which is read-only.

### 4.3 Timing-dependent case — window reopen guard (7.11)

`CancelAsync` refuses to reopen a window early while **another live window still covers `now`**. Because `POST /maintenance-windows` requires `start >= now`, a window can never be created already covering `now`; the guard becomes observable only after window 1 has started.

- Request 7.2 creates window 1 starting **45 seconds** after the prerequest runs.
- Request 7.10 cancels window 3, which has not started yet -> expected `200`.
- Request 7.11 tries to cancel window 2 while window 1 covers `now`.
  - If more than 45 seconds have passed since 7.1 (usual for a human run, and likely for a full Newman run), the guard returns **`409 STRUCTURE_IN_USE`**.
  - If it is still too early, the script logs `SKIP: got HTTP 200 ... wait until <windowStart1> and re-send`. Re-send 7.11 after `windowStart1` passes to observe the 409. This is a **Blocked** observation until re-sent, not a pass.

## 5. Code-vs-contract discrepancies (design v1.3 vs `Program.cs`)

Each row states the design claim, the implemented behavior, and what the collection asserts. The collection follows the code.

| # | Area | API-design v1.3 | Code / implemented | Followed |
|---|---|---|---|---|
| 1 | `GET /api/parking-lots` | line 55: includes inactive/maintenance lots with `status` **and derived `serviceState`** | `Program.cs:157` returns `SiteProfile[]` only: `id, tenantId, code, name, address, latitude, longitude, isActive, totalPhysicalCapacity, status`. **No `serviceState`**. `serviceState` exists only on `GET /{id}/structure` (`Program.cs:179-191`). A `MAINTENANCE` lifecycle status is never written, so it never appears here. | 1.3 asserts a `status` string on every row; 1.5 asserts the deactivated row is returned with `status:INACTIVE`. The missing list-level `serviceState` is logged, not asserted. |
| 2 | `POST /{id}/slots` response | §"slot registration": `201 {success,data:{slotId,code,created,released}}`; idempotent live replay -> `200 created:false` | `Program.cs:247` returns `200 {success,data:"<slotId>"}`. A live duplicate code is rejected by `ParkingStructure.AddSlot` with `409 DUPLICATE_IDENTIFIER`; there is **no `created` flag** and no idempotent `created:false` replay on this route. | 3.1/3.2 assert `200` + string id; 3.3 asserts `409 DUPLICATE_IDENTIFIER`. |
| 3 | `409 IDENTIFIER_RELEASED` body | §"slot registration": `released { slotId, code, deletedAt }` | `Program.cs:83` emits `released { id, code, deletedAt }` — the key is **`id`**, not `slotId`. Same shape for units/paths (the released value is the unit name / path code). | 3.10 asserts `released.id`, `released.code`, `released.deletedAt`. |
| 4 | Maintenance window request | §"maintenance windows": `POST {start,end,unitId?,slotId?,reason}`; DE-3 rejects scoped windows with `400 INVALID_STRUCTURE` | `MaintenanceWindowBody` (`Program.cs:310`) is `(Start, End, Reason, TenantId)` — it has **no `unitId`/`slotId`**, and `Program.cs:277` always passes `null, null`. A scoped window cannot even be expressed on the wire; `MaintenanceWindowRules.ValidateCreate` still guards it for direct callers. | 7.2/7.5/7.9 send only `start/end/reason/tenantId`. |
| 5 | `GET /{id}/structure` body | §"structure read": typed DTO with `serviceState`, a top-level `capacity{registeredPhysicalCount,configuredLimit,operationalAvailable,basis,projectionState,reason}`, `slots[].primaryVehicleType`, `slots[].compatibilitySource` | `Program.cs:168-193` serializes the **domain aggregate** (`Site, Units, Slots, Paths, BackupPolicies, CapacityViews`) plus `serviceState`. There is no top-level `capacity` block. Slots expose `vehicleType` (the projection column) and `vehicleTypes` (compatibility) but **no `primaryVehicleType` / `compatibilitySource`**. `capacityViews[].available` can still throw `CAPACITY_CONFLICT` during serialization (design DE-8 asks for `null` + `reason` instead). | 6.1 asserts the seven keys the code actually returns and the `serviceState` shape. |
| 6 | Legacy whole-structure ingest | Q1 / DE-9 reject destructive replace-all ingestion | No `POST /api/parking-lots/{id}/structure` is mapped; only `GET` (163) and `PATCH` (248). ASP.NET returns **405**. The implemented narrow write route is `POST /{id}/structure/batch`. | 1.6 asserts `404`/`405` and no success envelope. |
| 7 | Batch `Idempotency-Key` | required header; items restricted to the deactivation family; cross-lot item -> per-item `NOT_FOUND`, whole batch `409` | Implemented as specified (`Program.cs:287-295`, `StructureBatchException`). The header must parse as a **GUID**; a non-GUID or missing header is `400 INVALID_STRUCTURE`. A body `idempotencyKey` that disagrees with the header is `400`. | 8.1 missing header -> 400; 8.4 cross-lot -> `items[].code = NOT_FOUND`; 8.7 mismatch -> 400. |
| 8 | `removeUnit` empty check | §"hierarchy": non-empty unit -> `409 UNIT_NOT_EMPTY` | Implemented in `ParkingStructure.RemoveUnit`. | 8.5 uses a unit that still has a ZONE child to force `UNIT_NOT_EMPTY` and prove all-or-nothing. |
| 9 | `configureSlot` geometry | `coordinates3D`/`features` non-null -> `400 INVALID_STRUCTURE` | Implemented in `ParkingStructure.ConfigureSlot` (`Require(coordinates3D is null && features is null)`). `EditStructure` is **not** `Disallow`-annotated, so the fields bind and are then rejected by the domain. `coordinates3D`/`features` are not parsed as JSON by this action (only `paths.mapData` is validated as JSON). | 5.11 / 5.12 send plain non-null strings and assert 400. |
| 10 | Maintenance enforcement | window rows become effective only after an external acknowledgement; reads use `MAINTENANCE_UNCERTAIN` until then (DE-13) | Implemented: every create/extension writes `enforcement_state='pending'`, and `MaintenanceWindowState.Derive` returns `MAINTENANCE_UNCERTAIN` for a current `pending` window. | 7.2/7.5/7.7 assert `enforcementState = pending`. |

## 6. Case → acceptance map

| Acceptance axis | Requests |
|---|---|
| Lot create (201 + `Location`) | 1.1 |
| Lot list includes inactive/maintenance rows, `status` field | 1.3, 1.4, 1.5 |
| Legacy replace-all ingest rejected | 1.6 |
| Hierarchy FLOOR → ZONE → BLOCK | 2.1, 2.2, 2.3 |
| Duplicate identifier / hierarchy-order negatives | 2.4, 2.6 |
| Slot single category | 3.1 |
| Slot compatibility set + primary category | 3.2 |
| Live duplicate slot | 3.3 |
| Released code `IDENTIFIER_RELEASED` + explicit restore | 3.8, 3.9, 3.10, 3.11 |
| Access path create | 4.1, 4.2 |
| Every PATCH action: profile, active, renameUnit, capacity, removeUnit, removeSlot, moveSlot, configureSlot, removePath, backup, markBackup, restoreSlot, restoreUnit, restorePath | 5.1–5.17 |
| `configureSlot` geometry rejection (A9 / F13) | 5.11, 5.12 |
| Unknown action rejected | 5.18 |
| `GET /structure` keys + `serviceState` | 6.1 |
| `GET /operational-layout` (SPARK-228) + bad floor | 6.2, 6.3 |
| `GET /operator-layout` assigned-operator audience | 6.4, 10.9, 10.10 |
| Maintenance list / create / overlap / extend-only / cancel | 7.1–7.11 |
| Batch header required + all-or-nothing + per-item failure | 8.1–8.7 |
| Operations create / read / idempotent replay | 9.1–9.4 |
| 401 missing bearer (read + write + operator-layout) | 10.1, 10.2, 10.3, 10.9 |
| 403 foreign tenant without granting it | 10.4, 10.5, 10.6, 10.10 |
| 404 foreign/missing lot without existence leak | 10.7, 10.8 |

## 7. Reading a FAIL

Open the failed request → **Test Results** and read the assertion message.

| Symptom | Likely cause / fix |
|---|---|
| 0.1–0.4 return 401 | Wrong `.env` password, or the email default does not match `.env`. The assertion message includes the first 200 chars of the response. |
| 1.1 creates a lot but 1.2/1.3 fail | `runSuffix` empty (request 0.1 did not run or failed). Re-run folder 00 first. |
| Structure write returns 503 `COMMITMENTS_UNAVAILABLE` | ReservationService is not running. Start it (port 5055). |
| Structure write returns 503 `IDENTITY_UNAVAILABLE` | UserService is not running (port 5035). |
| 3.10 returns `409 DUPLICATE_IDENTIFIER` instead of `IDENTIFIER_RELEASED` | The slot code was never released, or a live slot with that code already exists. Re-run the whole Slots folder from 3.8; do not run 3.10 standalone. |
| 6.1 fails on `serviceState` | Wrong ParkingService/FacilityService build. The additive `serviceState` is emitted at `Program.cs:179-191`. |
| 6.4 returns 403 | The operator is not assigned to `seededSiteId` with `SLOT_OVERRIDE`. Re-run `./scripts/seed-demo-site.ps1`; verify `GET {{userServiceUrl}}/api/operators/slot-scope?siteId={{seededSiteId}}` returns 200 with `operatorToken`. |
| 7.6 / 7.8 do not return 409 | The window end may already be in the past, or reservation/clock skew. Re-run folder 07 from 7.1 (timestamps are regenerated). |
| 7.11 logs `SKIP` | Expected before window 1 starts. Wait until `windowStart1` and re-send 7.11. See §4.3. |
| 8.2 returns 409 instead of 200 | `batchSlotId` or `batchPathId` is missing (folder 03/04 not run), or a prior run left a live commitment on the site. Use a fresh run. |
| 8.5 returns a different `items[].code` | The floor already lost its ZONE child (a prior `removeUnit` on the zone). Re-run from folder 02. |
| 9.1 returns 202 `pending` | ReservationService impact endpoint is unavailable, so the operation is pending, not completed. Start ReservationService and retry with the same `operationKey` (replay semantics). |
| 10.4–10.6 return 404 instead of 403 | `foreignTenantId` accidentally equals the caller's tenant. It must be a well-formed but unauthorized tenant id. |

## 8. What was NOT verified

This task authored artifacts only. It did **not** start the app, Docker, or any service, and it did **not** run `dotnet`. The following remains unverified at runtime:

- **No request in this collection was executed.** Status codes, response bodies and the timing behavior in §4.3 are derived from a static read of `Program.cs`, the domain/application/persistence sources, and the existing route-level xUnit suite `tests/Services/SmartParking.FacilityService.Tests/FacilityStructureEndpointTests.cs` (which uses in-process fakes, not a running stack).
- **`403` on `GET /{id}/operator-layout` with a driver token** depends on UserService's slot-scope endpoint rejecting an operator-only scope; the mapping is read from `SlotOperatorClient`, not observed live.
- **`INVALID_FLOOR`** (6.3) is carried over from verified SPARK-228 evidence, not re-observed here.
- **The reopen guard 409** (7.11) is timing-dependent and can only be observed after a window start (see §4.3).
- **`configureSlot` capacity side effects**: 5.10 moves a CAR slot to the MOTORCYCLE pool; on a heavily seeded site this can interact with backup arithmetic. The collection uses a freshly created lot, so this is expected to be benign but is not proven.
- **Static JSON validity was checked** (parse + request-body parse + folder/request counts) with PowerShell `ConvertFrom-Json`; Postman/Newman syntax semantics were not executed.

## 9. Source evidence used

| Topic | Evidence |
|---|---|
| Route paths, request records, response envelopes | `src/Services/FacilityService/SmartParking.FacilityService.API/Program.cs` (routes 111–296, records 300–314) |
| Error → status mapping, released/batch envelopes | `Program.cs:61-110` |
| Unit/slot/path/restore/batch domain rules | `Domain/ParkingStructure.cs`, `Domain/StructureBatch.cs`, `Domain/MaintenanceWindow.cs` |
| PATCH action dispatch | `Application/StructureEditHandler.cs` |
| Primary-category rule | `Application/SlotCompatibility.cs` |
| Operations + idempotency | `Application/StructureOperations.cs`, `Persistence/StructureOperationStore.cs` |
| Maintenance overlap / extend-only / cancel guard | `Persistence/PostgresMaintenanceWindowStore.cs`, `Application/MaintenanceWindows.cs` |
| Released-identifier detection | `Persistence/PostgresParkingStructureRepository.cs:150`, `:173`, `:205`, `:241-278` |
| Owner/operator scope and 401/403 | `Infrastructure/ServiceClients.cs:9-34`, `Infrastructure/SlotOperatorClient.cs` |
| Backup permission grants (`BACKUP_CONFIGURE` / `BACKUP_MARK`) | `src/Services/UserService/.../OwnerScope.cs`, `IdentityWorkflowStore.cs:171` |
| Route-level behavior cross-check (fakes) | `tests/Services/SmartParking.FacilityService.Tests/FacilityStructureEndpointTests.cs` |
| Expected codes reference | `docs/03-design/api/parking-structure-api-design.md` v1.3 |
