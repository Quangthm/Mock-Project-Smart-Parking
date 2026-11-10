# SPARK-244 Realtime Operator Layout Monitoring — Manual Test Guide

Manual verification run-book for the SPARK-244 operator realtime monitoring slice, authored against branch `feat/SPARK-244-realtime-monitoring`. This is the human companion to:

- `SmartParking-Realtime-Layout-SPARK-244.postman_collection.json`
- `SmartParking-Realtime-Layout-SPARK-244.postman_environment.json`
- `test-plans/SPARK-244-branch-verification-test-guide.md`

Route paths, request bodies, response fields and status codes come from the worktree source, not from the design summary. Where the design/OpenAPI disagrees with the code, the code is followed and the difference is listed in **section 6**.

> **Bắt đầu từ đâu (tiếng Việt):** nếu bạn muốn *chạy test theo từng bước*, mở `../test-plans/SPARK-244-branch-verification-test-guide.md` — checklist C1–C10, mỗi case có **Điều kiện trước / Các bước / PASS / FAIL / BLOCKED** kèm lệnh PowerShell và SQL kiểm tra DB. Tài liệu tiếng Anh này là phần tham chiếu bổ sung: mapping case → acceptance (§5), bảng lệch code-vs-contract (§6), cách đọc một FAIL (§7), và những gì đã/chưa verify (§9, §10).

> **Run status.** The four artifacts were authored first, then **executed live** against a stack built from this branch: **33/33 requests, 33/33 test-scripts, 101/101 assertions, 0 failures** (Newman 6.2.3). Passwords are never committed — they are supplied at run time from the repository `.env`. Section 9 records exactly what the live run proved and what is still unverified; section 10 lists the cases that remain BLOCKED.

## 1. Purpose and scope

SPARK-244 adds an operator realtime channel on top of the SPARK-228 operational-layout read path:

1. Operations is the occupancy source of truth; FacilityService is a projection consumer (`delta design` G2).
2. A transactional outbox in OperationService publishes occupancy events (G5).
3. A Facility consumer projects the events onto `parking_slots` with `processed_events`, `slot_sequence_watermarks` and `occupancy_release_ledger` (G3/G5).
4. A SignalR hub (`/ws/parking-lots/{siteId}/layout`) authenticates an assigned operator and pushes deltas to group `site:{siteId}:operators` (G6).
5. The snapshot carries zone-only precision without inventing a slot (G4).

In scope here: the REST surfaces that can be driven and observed without a WebSocket client, plus DB-only checks for the consumer guarantees.

Out of scope: the 3D front end, requested-vs-allocated rendering, payment behavior, and production SLA. Those remain deferred/P3+ and are not upgraded into mandatory MVP items by this guide (see `docs/04-testing/README.md`).

## 2. Artifacts

| File | Contents |
|---|---|
| `postman/SmartParking-Realtime-Layout-SPARK-244.postman_collection.json` | Postman v2.1.0, **7 folders / 33 requests** |
| `postman/SmartParking-Realtime-Layout-SPARK-244.postman_environment.json` | **34 variables**; real demo emails, empty secret slots, no committed secrets |
| `postman/SPARK-244-manual-test-guide.md` | This run-book |
| `test-plans/SPARK-244-branch-verification-test-guide.md` | Checklist with VERIFIED / TO RUN / BLOCKED |

Collection schema: `https://schema.getpostman.com/json/collection/v2.1.0/collection.json`.

| Folder | Requests | Concern |
|---|---:|---|
| `00 - Setup (run first)` | 4 | Login owner / operator / admin / driver; generate `runSuffix`, `walkInPlate`, `probePlate` |
| `01 - Operator layout snapshot` | 6 | Operational-layout contract, filters, anonymous sanitized read, 404 |
| `02 - Outbox / idempotency` | 5 | Walk-in check-in/check-out writes `outbox_events`; duplicate-plate guard |
| `03 - Release-once` | 6 | Internal claim, double release, slot state restored |
| `04 - Zone-only reserved count` | 2 | `zoneOnlyReservedCount` shape and reader invariant |
| `05 - Operator scope` | 6 | `site-scope`, 403 `OPERATOR_LOT_UNASSIGNED`, role gate, legacy layout gate |
| `06 - Realtime delivery / latency` | 4 | REST read/write latency proxies only |

## 3. Prerequisites

- Docker Desktop + PowerShell. Newman 6.2.3 is available through `npx --yes newman`, so no global install is required.
- Worktree `.env` at the repository root (never commit it). It **must** define the UserService CORS origins — after the develop merge CORS is config-driven (`Program.cs:24-31` reads `Cors:AllowedOrigins`) and `compose.app.yml:54` maps it from `${SMARTPARK_USER_CORS_ORIGINS:-}`, i.e. **empty by default**:

```text
SMARTPARK_USER_CORS_ORIGINS="http://localhost:5173;http://127.0.0.1:5173;http://localhost:8443;http://127.0.0.1:8443"
```

Without it, preflight returns `204` with **no `Access-Control-Allow-Origin`**, the browser blocks the login `fetch`, and the FE reports "Cannot reach the authentication server. Please try again." while direct curl/Postman calls still succeed.

- Run both stacks from the worktree. The app stack declares the schema network as external (`smartpark-schema_default`), so `rabbitmq` and `redis` already resolve by service name:

```powershell
docker compose --env-file .env -f compose.schema-integration.yml up -d   # 6 databases + redis + rabbitmq
docker compose --env-file .env -f compose.app.yml build                  # user / parking / reservation / payment / frontend
docker compose --env-file .env -f compose.app.yml up -d
```

Ports: UserService `5035`, FacilityService `5045`, OperationService `5055`, PaymentService `5065`, FE `5173`.

- Fresh volumes apply the SPARK-244 overlays automatically through the init mounts (`08-realtime-projection.sql` on facility-db, `06-overlay-realtime-outbox.sql` on operations-db). For an **existing** volume apply them incrementally — the script is idempotent and also asserts the four required relations:

```powershell
./scripts/apply-workflow-migrations.ps1 -Service Parking   -SchemaRoot .
./scripts/apply-workflow-migrations.ps1 -Service Operation -SchemaRoot .
```

Expected: `schema already current; applied incremental features and verified required relations`. Both `-Service` and `-SchemaRoot` are mandatory, and `-Service Operation` maps internally to the `Reservation` scripts/`operations-db`.

- Seed **after** the user-service is up: `./scripts/seed-demo-site.ps1`, then run `./scripts/repair-demo-identity.ps1 -Apply` (the `DataSeeder` never adds `driver@gmail.com` once any user exists — see §9.1). The seed creates `DEMO-HQ` (`b0000000-0000-4000-8000-000000000001`), tenant `a0000000-0000-4000-8000-000000000001`, Floor 1, Zone A and the CAR/EV slots, and assigns the demo operator with `SLOT_OVERRIDE`.

- Fill `serviceKey` in the environment from `.env` `SMARTPARK_SERVICE_KEY` (compose maps it to `Services:Key`). The key must be at least 32 characters. Every `/internal/**` route returns **401** on a missing or wrong `X-Service-Key`.

### 3.1 Consumer and backplane are ON in the docker stack

| Feature | Gate in source | Current value in this worktree |
|---|---|---|
| Facility occupancy consumer | `RabbitMq:ConnectionString` (`FacilityService/API/Program.cs`) | **ON** — `parking-service` has `RabbitMq__ConnectionString` from `compose.app.yml`; `OccupancyProjectionWorker` is registered |
| Outbox publisher | `RabbitMq:ConnectionString` (`InfrastructureDependencyInjection.cs`) | **ON** — `reservation-service` has `RabbitMq__ConnectionString`; events are published and `published_at` is set |
| SignalR Redis backplane | `Redis:ConnectionString` or `ConnectionStrings:Redis` (`OperationService/API/Program.cs`) | **ON** — `reservation-service` runs with `Redis__ConnectionString=${SMARTPARK_REDIS_CONNECTION:-}`, and `.env` sets `redis://redis:6379`; `smartpark-redis` is healthy |

If you start the services without those variables, the consumer and publisher silently fall back (projection tables untouched / `DisabledOutboxEventPublisher`) and folder 02 can only be checked from the **write** side.

## 4. Import and run

1. Import both JSON files into Postman.
2. Select **SmartParking - SPARK-244 Realtime Layout (local)**.
3. Fill the secret variables locally:
   - `ownerPassword` ← `.env` `SMARTPARK_OWNER_PASSWORD`
   - `operatorPassword` ← `.env` `SMARTPARK_OPERATOR_PASSWORD`
   - `adminPassword` ← `.env` `SMARTPARK_ADMIN_PASSWORD`
   - `serviceKey` ← `.env` `SMARTPARK_SERVICE_KEY`
   - `driverPassword` already ships the public seeded value.
   - Override `ownerEmail` / `operatorEmail` / `adminEmail` only if `.env` uses different `SMARTPARK_*_EMAIL` values.
4. Leave `runSuffix`, the captured ids and the latency variables empty; they are generated at run time.
5. Run **00 - Setup** first. It must be green: it stores all four tokens and the plate values.
6. Run folders 01 → 06 in order. Keep **Stop run on error** off for a diagnostic first pass.

Folder 03 mutates one seeded slot (claim → OCCUPIED, release → AVAILABLE). Folders 02 and 06 create walk-in sessions and check them out again. If a run is interrupted, run 3.4/3.5 and 2.4/6.4 manually to restore the seeded site.

### 4.1 Data prerequisites per case

| Case | Needs |
|---|---|
| 1.x | Seeded site plus a running UserService (owner login) and FacilityService |
| 2.1-2.4 | Operator assigned to `seededSiteId`; operations DB with `outbox_events` |
| 3.x | `serviceKey` filled; at least one AVAILABLE slot on the seeded site |
| 4.x | Seeded site with a ZONE; no zone-level reservation table exists |
| 5.1 / 5.5 | Operator assigned to `seededSiteId` with `SLOT_OVERRIDE` |
| 5.2 / 5.3 | `foreignSiteId` must not be assigned to the operator |
| 6.x | Operator assigned; operations DB schema present |

### 4.2 FE manual run-book (operator realtime — covers C10)

Prerequisites: §3 is satisfied (CORS variable present), operator login works, seeded site selected. The FE hub client never patches slot state locally — it validates the envelope through `operatorRealtimeGuard` and then triggers a **REST snapshot refetch**, so every visual change must be traceable to an authoritative `GET .../operational-layout`.

1. Log in as `operator@smartpark.local` at `http://localhost:5173` and open the dashboard.
2. Select `DEMO-HQ`. In DevTools → Network expect (a) `GET /api/parking-lots/{siteId}/operational-layout` → `200`, (b) `POST http://localhost:5055/ws/parking-lots/{siteId}/layout/negotiate?negotiateVersion=1` → `200`, then the `ws://` upgrade → `101`. Console must be clean.
3. On connect the hub sends `SnapshotRequired`; the client resets the guard and refetches. Confirm exactly one extra snapshot GET appears right after the upgrade.
4. **Delta delivery:** with the dashboard open, run Postman folder `02` (walk-in check-in/check-out) or folder `03` (claim/release). Expect a new `operational-layout` GET within ~2 s, the affected slot/occupancy changing **without** a full page reload, and no flicker.
5. **Reconnect:** `docker compose --env-file .env -f compose.app.yml stop reservation-service`, observe retry attempts (2 s after close, 10 s after a failed start), then `start reservation-service`. Expect the hub to reconnect and a fresh snapshot to replace the previous one (not merge).
6. **Token lifecycle:** leave the tab idle until the access token nears expiry — the `authRefreshedEvent` path must keep the hub connected; then log out and confirm the hub stops with no reconnect loop in the console.
7. **Negative scope:** log in as `driver` — the operator dashboard/hub must not be used, no hub connection to `/ws/parking-lots/...` should appear.
8. Record per case: WS connect status, write→snapshot-refresh time, whether the snapshot was refetched (not patched), console errors, and any visual flicker. A failing step maps to C10/C11/C12 in the branch-verification guide.

## 5. Case → acceptance map

### 5.1 Request cases

| Case | Method + path | Expected | Acceptance |
|---|---|---|---|
| 0.1 | `POST /api/auth/login` (owner) | 200, `data.accessToken`, role `owner`; captures token + plate values | prerequisite |
| 0.2 | `POST /api/auth/login` (operator) | 200, role `operator` | prerequisite |
| 0.3 | `POST /api/auth/login` (admin) | 200, role `admin` | prerequisite |
| 0.4 | `POST /api/auth/login` (driver) | 200, role `driver` | prerequisite |
| 1.1 | `GET /api/parking-lots/{seededSiteId}/operational-layout` (owner) | 200; site + contract metadata + metrics + zones + floors + slot visual states; **no** slot `eventId` / `sequenceNumber` / `siteId` — by design, those live on the realtime envelope (`RealtimeEventEnvelope`), not on the snapshot | G1 (partial), G4 shape, snapshot contract, G5 (envelope-only anchor) |
| 1.2 | same + `?vehicleType=CAR` | 200; every `slots[].supportedVehicleType = CAR`; metrics still present | snapshot contract |
| 1.3 | same + `?vehicleType=BOGUS` | 400 `INVALID_VEHICLE_TYPE` | validation |
| 1.4 | same + `?floor=__NOPE__` | 400 `INVALID_FLOOR` | validation |
| 1.5 | same, no bearer | 200 sanitized (not 401) | SPARK-228 public read |
| 1.6 | same for `foreignSiteId`, no bearer | 404 `SITE_NOT_FOUND` | negative |
| 2.1 | `POST /api/parking-sessions/walk-in/check-in?siteId={seededSiteId}` | 200; `data.id`, `licensePlate`, `vehicleType=CAR`, `slotId=null`, `slotNumber=null` | G2 (write), G5 envelope |
| 2.2 | `GET /api/parking-sessions?siteId={seededSiteId}` | 200; session row with `status=ACTIVE`, `originalPlate`, `entryTime` | G2 (read-back) |
| 2.3 | repeat 2.1 with the same plate | 409 `VEHICLE_ALREADY_INSIDE` | duplicate guard |
| 2.4 | `POST /api/parking-sessions/{id}/walk-in/check-out?siteId={seededSiteId}` | 200; `data.id` echoes, `exitTime` set | G2/G3 write |
| 2.5 | `GET .../operational-layout` | 200; snapshot still readable after outbox writes | G2 projection unaffected by no-slot event |
| 3.1 | `POST /internal/gate-sites/{seededSiteId}/slots/claim` **without** `X-Service-Key` | 401 | internal auth |
| 3.2 | same **with** `X-Service-Key` + `{tenantId, vehicleType:CAR}` | 200; `{id, code, vehicleType}`; captures slot id | release-once setup |
| 3.3 | `GET .../operational-layout` | claimed slot `physicalState=OCCUPIED` | G2 projection |
| 3.4 | `POST /internal/gate-sites/{seededSiteId}/slots/{id}/release` | 204, empty body | G3 |
| 3.5 | repeat 3.4 | 204, empty body (second UPDATE matches zero rows) | G3 release-once at API boundary |
| 3.6 | `GET .../operational-layout` | slot `physicalState=AVAILABLE`, `reservationState=UNRESERVED`, `isBookable=true` | G3 |
| 4.1 | `GET .../operational-layout` | every `metrics.zoneCapacities[]` has numeric `allocatedSlotCount` and `zoneOnlyReservedCount` | G4 shape |
| 4.2 | same | `zoneOnlyReservedCount` equals the RESERVED slot count in that zone; `allocatedSlotCount` mirrors it | G4 reader semantics |
| 5.1 | `GET /api/operators/site-scope?siteId={seededSiteId}` (operator) | 200, `{userId, tenantIds:[tenantId]}` | G6 |
| 5.2 | `GET /api/parking-sessions?siteId={foreignSiteId}` (operator) | 403 `OPERATOR_LOT_UNASSIGNED` | G6 REST mapping |
| 5.3 | `GET /api/operators/site-scope?siteId={foreignSiteId}` (operator) | 403 `FORBIDDEN` (generic envelope; `OPERATOR_LOT_UNASSIGNED` belongs to 5.2) | G6 identity layer |
| 5.4 | same with driver token | 403 | G6 role gate |
| 5.5 | `GET .../operational-layout` as assigned operator | 200 (not 403) | G6 positive |
| 5.6 | `GET .../operator-layout` with driver token | 403 `FORBIDDEN` | SLOT_OVERRIDE gate |
| 6.1 | `GET .../operational-layout` | 200; `pm.response.responseTime ≤ 2000` (proxy) | Q4 proxy |
| 6.2 | walk-in check-in (probe) | 200; `data.id` captured | latency write |
| 6.3 | `GET /api/parking-sessions` | probe session visible; elapsed `≤ 2000` (proxy) | Q4 proxy |
| 6.4 | walk-in check-out (probe) | 200; `exitTime` set | cleanup |

### 5.2 Acceptance criteria and planning traceability

| Acceptance | Covered by | Result |
|---|---|---|
| G1 requested vs allocated | 1.1 | **NOT IMPLEMENTED in the projection**: `operatorContext` is always `null`; no requested/allocated fields exist. Asserted as null/absent, not PASS. |
| G2 Operations is the occupancy authority | 2.1-2.5 | REST write path verified by assertion design; consumer projection is DB-only |
| G3 release-once | 3.4-3.6 + SQL | API-layer idempotency covered; consumer ledger DB-only |
| G4 zone-only precision | 4.1-4.2 | Contract shape covered; true zone-only creation BLOCKED |
| G5 duplicate/replay | 2.3 + SQL | Duplicate plate covered; event replay BLOCKED |
| G6 assigned scope | 5.1-5.6 | REST scope and mapping covered; hub BLOCKED |
| P4 4.1 reconnect snapshot | 1.1 + manual | BLOCKED (no WS client) |
| P4 4.2 duplicate event no-op | SQL only | BLOCKED (needs broker/DB harness) |
| P4 4.3 other-scope operator rejected | 5.2, 5.3, 5.4 | covered by assertion design |
| P4 4.4 zone-only no invented slot | 4.1, 4.2 | partial; true case BLOCKED |
| P4 4.5 release-once | 3.4-3.6 + SQL | partial (consumer ledger DB-only) |
| P4 4.6 p95 latency | 6.1, 6.3 | proxy only; true p95 BLOCKED |
| A1 snapshot under 2s | 6.1 | proxy only |
| A2 delta without reload | 6.x + manual | BLOCKED (SignalR) |
| A3 reconnect full snapshot | 1.1 + manual | BLOCKED (SignalR) |
| A4 duplicate event unchanged | SQL | BLOCKED |
| A5 foreign operator 403 | 5.2 | covered |
| A6 zone-only "chưa xác định ô" | 4.1/4.2 | partial (no zone-only source) |
| A7 requested vs allocated separate | 1.1 | NOT IMPLEMENTED |
| A8 release exactly once | 3.4-3.6 | partial |
| A9 allocation/physical/visual separated | 1.1, 3.3, 3.6 | contract-visible |
| A10 check-in only from Operations | 2.1/2.2 | partial (walk-in path) |
| A11 payment never releases | — | out of scope here (payment service not involved) |
| A12 no client mutation authority | — | out of scope (no client writes in these artifacts) |

## 6. Code-vs-contract discrepancies

Each row states the design/OpenAPI claim, what the source actually does, and what the artifacts assert. The collection follows the code.

| # | Area | Design / OpenAPI | Code / implemented | Followed by |
|---|---|---|---|---|
| 1 | Per-slot idempotency fields | **RESOLVED (doc aligned to code).** The OpenAPI used to declare `eventId`, `sequenceNumber`, `siteId` on `SlotVisualStateDto`; those properties have been removed, so `SlotVisualStateDto` now matches `OperationalLayoutProjection.cs:60-77` (slots carry only structural/state fields). The three fields remain only on `RealtimeEventEnvelope`, per G5 | `SlotVisualStateDto` in `OperationalLayoutProjection.cs:60-77` has no such fields; the envelope carries `eventId` / `sequenceNumber` / `siteId` | 1.1 asserts their **absence** on slots — this is now the canonical contract, not a discrepancy; the idempotency anchor is the envelope |
| 2 | G1 requested vs allocated | `operatorContext.approachingReservation {requestedSlotCode, allocatedSlotCode, isReallocated, ...}` | `PostgresOperationalLayoutReader.Project` always sets `OperatorContext = null` and `SessionType = null` (lines 226-233); no requested/allocated source | 1.1 asserts `operatorContext` exists and is null; A7 is not testable through REST |
| 3 | G2 event name/shape | check-in publishes `OccupancyChanged {slotId, physicalState:OCCUPIED, sessionId, ...}` | reservation check-in emits `SlotStateChanged {reservationId, sessionId, slotId, physicalState}` (`ReservationGateStore:224-229`); walk-in emits `OccupancyChanged {sessionId, action:checkin_walkin}` with **no slotId** (`:102-107`); releases emit `OccupancyReleased` | 2.1/2.4 drive the walk-in path and assert `slotId=null`; a slot-level `SlotStateChanged` needs a seeded reservation (BLOCKED) |
| 4 | G5 sequence scope | `sequenceNumber` is monotonic **per slotId** (delta design §6, OpenAPI line 646) | `NextSequenceAsync` is `max+1` **per siteId** (`ReservationGateStore:305-311`); the `(site_id, sequence_number)` index is not unique; consumer watermark is per slot (`OccupancyProjectionRepository:63-71`) | SQL checks document the per-site sequence and per-slot watermark; concurrent-producer race is a recorded follow-up |
| 5 | Outbox schema | delta design §9.3 proposes `aggregate_type`, `aggregate_id`, `retry_count` | `realtime-outbox.sql` has only `event_id, event_type, occurred_at, sequence_number, site_id, payload, published_at` | DB-only SQL uses the actual columns |
| 6 | G6 REST parity | "operator without assignment returns 403 `OPERATOR_LOT_UNASSIGNED` on `/operational-layout`" (delta design §7) | `LayoutCallerResolver.ResolveOperatorAsync` catches `FORBIDDEN` and returns an Operator caller with no tenant/telemetry; the read proceeds **200 sanitized**, not 403 | 5.5 asserts 200 for an assigned operator; the unassigned 403 is asserted through OperationService (`5.2`) and raw UserService (`5.3`) |
| 7 | Hub `SnapshotRequired` payload | `{siteId, reason:"reconnect"}` (delta design §8) | `Clients.Caller.SendAsync("SnapshotRequired", siteId)` sends a single `siteId` argument (`ParkingLotLayoutHub:38`) | No WS request exists; documented BLOCKED |
| 8 | Hub event catalogue | OpenAPI enum includes `ZoneCapacityChanged`, `CapacityMetricsChanged`, `TelemetryFreshnessChanged` | No source emits those event types; only `SlotStateChanged`, `OccupancyReleased`, `OccupancyChanged` are written to the outbox | Documented; no assertion invented |
| 9 | G4 zone-only semantics | zone-level reservations increment `zoneOnlyReservedCount` while slots stay `UNRESERVED` | `PostgresOperationalLayoutReader:143-159` sets `allocatedSlotCount = zoneOnlyReservedCount = subtree RESERVED count`; there is no zone-level allocation source (OPEN D0-5) | 4.2 encodes that exact relation; a true zone-only case is BLOCKED |
| 10 | Anonymous/role visibility | Not specified for `/operational-layout` | Anonymous and driver get 200 sanitized; owner/admin/assigned operator get telemetry (`OperationalLayoutAccess.cs`, `OperationalLayoutRedaction.cs`) | 1.5 and 5.5 assert 200; no 401 is expected |

## 7. Reading a FAIL

Open the failed request → **Test Results** and read the assertion message. The login tests include the first 200 characters of the response.

| Symptom | Likely cause / fix |
|---|---|
| 0.1-0.4 return 401 | Wrong `.env` password, or the email default does not match `.env`. Do not paste secrets into the artifact. |
| 1.1 returns 404 `SITE_NOT_FOUND` | The demo seed was not run, or `seededSiteId` does not belong to the owner's tenant. Run `./scripts/seed-demo-site.ps1`. |
| 1.1 fails `contractVersion` / `stalenessThresholdSeconds` | Wrong service build. The snapshot contract is `1.2.0` / `30` (`OperationalLayoutContract`). |
| 1.1 fails `zones is not empty` | The seeded site has no ZONE unit. Re-run the facility demo seed. |
| 1.3/1.4 return 200 | Filter validation regressed; check `FacilityService/Program.cs:214-222` and `PostgresOperationalLayoutReader:164-169`. |
| 2.1 returns 503 `PROTOTYPE_SCHEMA_NOT_READY` | `realtime-outbox.sql` was not applied to operations-db, or `parking_sessions` is missing. Apply the overlay. |
| 2.1 returns 403 `OPERATOR_LOT_UNASSIGNED` | The operator is not assigned to `seededSiteId`. Re-run the demo seed. |
| 2.2 returns 200 but the session row is missing | The id was not captured (2.1 failed), or `walkInSessionId` was cleared. Re-run folder 02. |
| 2.3 returns 200 | The first check-in did not commit (2.1 failed) or the plate is not identical. |
| 3.1 returns 204/200 instead of 401 | `serviceKey` is empty and `Services:Key` is also empty? The service refuses to start without a key; otherwise the header is being injected globally. |
| 3.2 returns 401 | `serviceKey` is empty or does not match `SMARTPARK_SERVICE_KEY`. |
| 3.2 returns 409 `NO_AVAILABLE_SLOT` | No AVAILABLE slot on the seeded site (a prior run left one occupied). Run 3.4/3.5 against the old id or reseed. |
| 3.3 does not show `OCCUPIED` | Wrong slot id (3.2 did not capture), or the claim hit a different slot. Re-run folder 03. |
| 3.6 does not show `AVAILABLE`/`UNRESERVED` | Release failed, or another process claimed the slot between 3.5 and 3.6. |
| 4.2 fails the count relation | A reservation was created between the two reads, or the query hit a different zone. Re-run 4.1 then 4.2 immediately. |
| 5.1 returns 403 | The operator is not assigned or lacks `SLOT_OVERRIDE` (site-scope needs assignment only; slot-scope needs the permission). |
| 5.2 returns 200 | `foreignSiteId` accidentally matches an assignment, or the 403 mapping regressed in `OperatorIdentityClient.cs:41-42`. |
| 5.3 returns `code: OPERATOR_LOT_UNASSIGNED` | Wrong layer — that code is added by OperationService (5.2). 5.3 correctly returns the generic `FORBIDDEN` envelope. |
| 5.6 returns 200 | The legacy operator-layout `SLOT_OVERRIDE` gate regressed (`FacilityService/Program.cs:269-275`). |
| 6.1/6.3 exceed 2000 ms | Dev machine load. Q4 is a dev target, not an SLA; record the number instead of asserting a pass. |
| DB SQL returns no `outbox_events` rows | Overlay not applied, publisher disabled, or the write hit a different database. |

## 8. DB-only checks

Postman cannot run SQL. Run these manually and attach the output as evidence.

### 8.1 Operations DB (outbox)

```sql
-- Replace placeholders. Expect event_type = OccupancyChanged and payload->>'action' = checkin_walkin
SELECT event_id, event_type, occurred_at, sequence_number, site_id, payload, published_at
FROM outbox_events
WHERE payload->>'sessionId' = '<walkInSessionId>';

-- Envelope shape written by RabbitMqOutboxPublisher: eventId, eventType, occurredAt,
-- sequenceNumber, siteId, payload. published_at is set only after publish.
SELECT event_id, event_type, sequence_number, site_id, published_at
FROM outbox_events
WHERE site_id = '<seededSiteId>'
ORDER BY sequence_number DESC
LIMIT 20;
```

### 8.2 Facility DB (projection / idempotency)

```sql
-- G5: a duplicate eventId is recorded once and never re-applied
SELECT event_id, event_type, processed_at FROM processed_events ORDER BY processed_at DESC LIMIT 20;

-- G5: per-slot replay watermark
SELECT slot_id, last_sequence_number, updated_at FROM slot_sequence_watermarks ORDER BY updated_at DESC LIMIT 20;

-- G3: release-once ledger; this query must return zero rows
SELECT session_id, count(*) AS rows_for_session
FROM occupancy_release_ledger
GROUP BY session_id
HAVING count(*) > 1;

-- Claimed slot state (replace <claimedSlotId>)
SELECT id, slot_code, physical_state, reservation_state, updated_at
FROM parking_slots
WHERE id = '<claimedSlotId>';
```

### 8.3 Release-once / idempotency without Postman (covered by C4b)

The consumer's three protection layers — `processed_events`, `occupancy_release_ledger` and the `slot_sequence_watermarks` gate — live behind RabbitMQ and cannot be reached from Postman. They are covered by an **integration test** that calls `OccupancyProjectionRepository.ApplyAsync` directly against a throwaway PostgreSQL database:

`tests/Services/SmartParking.FacilityService.Tests/OccupancyProjectionRepositoryTests.cs` — 4 tests (duplicate `event_id`; a used release-ledger entry must **not** free a slot re-occupied by another session; a release with `slotId = null` is recorded but not projected; a stale sequence is ignored). Setup, command and expected results are in the branch-verification guide under case **C4b** (requires `SMARTPARK_STRUCTURE_TEST_CONNECTION`; without it the tests are skipped, not failed). Verified: `Passed: 4, Failed: 0`.

The older manual alternative — inserting a duplicate `event_id` into `processed_events` by hand — is still possible for a quick look, but it mutates test data, only exercises layer 1, and must not be run against a shared database. Prefer C4b.

## 9. What was verified live (and what was not)

### 9.1 Live run — executed

The collection was run with **Newman 6.2.3** against a stack **built from this branch** (facility and operations DBs recreated from the overlays, demo site seeded, `compose.app.yml` images rebuilt from the worktree, `redis` and `rabbitmq` up, all four `/health` endpoints 200, all four demo logins OK).

Command shape (secrets supplied from the repository `.env`, never committed):

```text
npx --yes newman run docs/04-testing/postman/SmartParking-Realtime-Layout-SPARK-244.postman_collection.json `
  -e docs/04-testing/postman/SmartParking-Realtime-Layout-SPARK-244.postman_environment.json `
  --env-var ownerPassword=… --env-var operatorPassword=… --env-var adminPassword=… --env-var serviceKey=… `
  --timeout-request 15000
```

Result: **33 / 33 requests, 33 / 33 test-scripts, 101 / 101 assertions — 0 failures, exit code 0**, total run 4.8 s, average response 58 ms (min 4 ms, max 344 ms).

Live evidence collected after the run:

| Query | Result |
|---|---|
| `SELECT event_type, count(*) FROM outbox_events GROUP BY …` | `OccupancyChanged=8`, `OccupancyReleased=8`, total 16 |
| `SELECT count(*) FROM outbox_events WHERE published_at IS NOT NULL` | 16 (publisher drained the table) |
| `SELECT count(*) FROM processed_events` (facility DB) | 16 (consumer applied every event exactly once) |
| `SELECT count(*) FROM occupancy_release_ledger` / `slot_sequence_watermarks` | 0 / 0 — expected, walk-in events carry `slotId=null` |

**Re-run after merging the latest SPARK-238 branch (merge `2e94684e`, 30 commits incl. the current `develop` with SPARK-248).** The entire schema stack was recreated from scratch (`down -v` on all schema volumes), the app images were rebuilt from the merged worktree, and the collection was executed again: **33/33 requests, 101/101 assertions, 0 failures, exit 0** (3.9 s, average 60 ms). DB evidence after that run: `outbox_events` 8 rows → 8 published / 0 pending, `processed_events` 8, `occupancy_release_ledger` 0 and `slot_sequence_watermarks` 0 (expected), 48/48 slots `AVAILABLE`.

Two prerequisites are easy to miss on a freshly rebuilt database:

1. `DataSeeder` creates admin/owner/operator but **not** `driver@gmail.com`, because `SeedAsync` returns immediately when any user row already exists (`DataSeeder.cs:26-29`) and the role bootstrap runs first. Run `scripts/repair-demo-identity.ps1 -Apply` after the user-service is up; without it `0.4 Login Driver` returns `401 AUTH_FAILED` and cases `5.4`/`5.6` fail as a cascade (7 assertion failures from one root cause in the first post-merge run).
2. `scripts/seed-demo-site.ps1` must run **after** the user-service has created the demo accounts, otherwise it stops with “Owner/operator demo accounts were not found in UserDb”.

### 9.2 Authoring defect found only by running

Eight test-scripts declared `const data = …`. `data` is a **Postman/Newman sandbox global**, so all eight threw `SyntaxError: Identifier 'data' has already been declared`. The identifier was renamed to `payload` in those scripts (string/comment-aware rename; `body.data` left intact) and the suite then passed. Static JS parsing cannot catch this — each script is valid JavaScript in isolation.

### 9.3 Still not verified

1. **No SignalR/WebSocket session was run.** Hub connect authorization, `SnapshotRequired`, group scoping, delta delivery and reconnect are unverified. Note that `POST …/negotiate` returns **200 anonymously** (the hub has no `[Authorize]`; the scope check runs in `OnConnectedAsync`), so negotiate is not a usable probe.
2. **No slot-bearing event was produced.** There is no reservation-creation REST route in this worktree, so `SlotStateChanged` / `OccupancyReleased` with a real `slotId` never occurred; the release ledger and the per-slot watermark were therefore never exercised.
3. **No duplicate/replayed delivery.** Needs a broker or DB harness; the collection only covers the duplicate-plate guard.
4. **No true zone-only reservation.** The reader approximates `zoneOnlyReservedCount` from subtree RESERVED slots (OPEN D0-5).
5. **No true p95.** 6.1/6.3 measure REST only (29–36 ms read, 179–204 ms write→read); the Operations → outbox → broker → consumer → SignalR leg is unmeasured.
6. **No Redis multi-instance fan-out.** The backplane is configured and `smartpark-redis` is healthy, but only one OperationService instance is running.
7. **No FE browser run yet.** `FE/src/lib/operatorLayoutLive.ts` is wired into `OperatorDashboard`, and `tsc --noEmit` / `vite build` are clean apart from the pre-existing 4 TypeScript errors, but no browser session has exercised the guard. Run §4.2 to close this one; it needs no new tooling.
8. **G1 requested-vs-allocated is not implemented** (`operatorContext` is always null) — recorded as not-implemented, not as a regression.

## 10. BLOCKED cases (live-only)

| # | Case | Exact reason | How to unblock |
|---|---|---|---|
| 03.7 | Consumer release-once ledger (G3/A8) — **Postman only** | Postman cannot produce a slot-bearing release: every walk-in event carries `slotId=null` and there is no reservation-creation REST route, so the ledger is never written from the collection | Covered by the **C4b** integration test (`OccupancyProjectionRepositoryTests.cs`, 4/4 pass) — see §8.3. For a live end-to-end check instead: seed a reservation with a slot, check it in/out through OperationService, then run the §8.2 ledger SQL |
| 02.6 | Replayed / duplicate event (G5/A4) — **Postman only** | Postman cannot publish to RabbitMQ or insert into PostgreSQL | Covered by the **C4b** integration test (duplicate `event_id` and stale-sequence cases) — see §8.3. For the full pipeline signal check, publish the same envelope twice with a broker harness, then query `processed_events` and `slot_sequence_watermarks` |
| 04.3 | True zone-only reservation (G4/A6) | There is no zone-level allocation source or REST route; the reader approximates `zoneOnlyReservedCount` from subtree RESERVED slots (`PostgresOperationalLayoutReader:143-159`, OPEN D0-5) | Requires a product decision/schema change; until then only the contract shape can be verified |
| 05.7 | Hub rejects an unassigned operator (G6/A5) | Postman cannot perform a SignalR/WebSocket handshake | Use a SignalR client: connect to `/ws/parking-lots/{foreignSiteId}/layout?access_token=<operator JWT>`; expect the connection to fail with `OPERATOR_LOT_UNASSIGNED` and no events |
| 06.5 | Hub `SnapshotRequired` on connect/reconnect (A3/P4 4.1) | Same WebSocket limitation | Connect a SignalR client, expect `SnapshotRequired`; disconnect/reconnect and confirm the client refetches `GET /operational-layout` and replaces (not merges) the snapshot |
| 06.6 | Live delta delivery (A2) | Same WebSocket limitation | With an assigned operator connected, trigger a reservation check-in; expect a `SlotStateChanged` callback with the outbox envelope and no visual flicker on replay |
| 06.7 | True p95 ≤ 2s (Q4/P4 4.6) | Needs 20+ samples over the complete Operations → outbox → broker → consumer → SignalR path; 6.1/6.3 measure REST only | Run a WS client, timestamp at the Operations write and at the client callback, compute p95 for ≥20 runs on the dev LAN |
| 06.8 | Redis backplane multi-instance fan-out (Q1/R1) | `smartpark-redis` is running and `reservation-service` has `Redis__ConnectionString=redis://redis:6379`, but only one OperationService instance exists | Start a second instance, connect one client to each, and confirm an event published on instance A reaches the client on instance B |

## 11. Source evidence used

| Topic | Evidence |
|---|---|
| Hub route, CORS, Redis gate, broadcaster registration | `src/Services/OperationService/SmartParking.OperationService.API/Program.cs:24-43` |
| Hub auth, error mapping, group, `SnapshotRequired` | `.../API/Hubs/ParkingLotLayoutHub.cs:9-40` |
| Site-scope client and 401/403/503 mapping | `.../Infrastructure/Clients/OperatorIdentityClient.cs:21-64`; interface `IOperatorIdentityClient.cs` |
| Outbox worker / envelope / routing key | `.../Infrastructure/Outbox/OccupancyOutboxWorker.cs`, `RabbitMqOutboxPublisher.cs:24-102` |
| SignalR broadcast envelope and group | `.../API/Realtime/SignalRLayoutRealtimeBroadcaster.cs:10-24` |
| Outbox writes and per-site sequence | `.../Persistence/ReservationGateStore.cs:78-158, 305-311` |
| Internal claim/release routes | `FacilityService/API/Program.cs:69-75, 128-157` |
| Operational-layout route and filters | `FacilityService/API/Program.cs:203-245` |
| Snapshot DTO and zone field | `FacilityService/Application/OperationalLayoutProjection.cs:8-97` |
| Snapshot composition / approximations | `FacilityService/Persistence/PostgresOperationalLayoutReader.cs:26-195` |
| Claim/release SQL | `FacilityService/Persistence/OperatorGateSlotStore.cs:9-94` |
| Consumer idempotency / release-once SQL | `FacilityService/Persistence/OccupancyProjectionRepository.cs:13-135`; contracts in `OccupancyProjectionRepositoryContracts.cs` |
| Consumer transport | `FacilityService/Infrastructure/OccupancyProjectionWorker.cs:12-128` |
| Redaction / access model | `FacilityService/Application/OperationalLayoutRedaction.cs`, `OperationalLayoutAccess.cs`, `Infrastructure/LayoutCallerResolver.cs` |
| UserService scope routes | `UserService/API/Controllers/OperatorsController.cs:26-44`; `OperatorSlotScope.cs` |
| SQL contracts | `scripts/database/overlay/realtime-outbox.sql`, `realtime-projection.sql` |
| Demo seed | `scripts/database/seeds/demo/01-identity.sql`, `02-facility.sql`, `03-reservation.sql` |
| Design intent | `docs/03-design/api/3d-digital-twin-spark-244-delta-design.md`, `3d-digital-twin-openapi.yaml` |