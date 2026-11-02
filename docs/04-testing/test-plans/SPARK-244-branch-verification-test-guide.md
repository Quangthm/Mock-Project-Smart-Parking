# SPARK-244 — Branch Verification & Manual Test Guide

Branch: `feat/SPARK-244-realtime-monitoring`
Scope: US-O03 / sprint-3 operator monitoring vertical slice — realtime layout deltas, assigned scope, release-once, zone-only precision, replay protection, dev latency target.
Contract: 3D Digital Twin `v1.2.0` + `3d-digital-twin-spark-244-delta-design.md` (G1–G6).
Companion: `postman/SPARK-244-manual-test-guide.md`, `postman/SmartParking-Realtime-Layout-SPARK-244.postman_collection.json`, `postman/SmartParking-Realtime-Layout-SPARK-244.postman_environment.json`.

> **Status legend**
> **VERIFIED** = the stated check was actually executed in this authoring task and the result is recorded below. Static checks are written `VERIFIED (static)`; no runtime check is marked VERIFIED.
> **TO RUN** = must be executed against a live branch stack; no result exists yet.
> **BLOCKED** = cannot be executed with the available tooling (Postman has no SignalR/WebSocket client; the branch consumer is off by default; no zone-level allocation source exists).

> **Status.** This guide started as a static review, then the branch was brought up locally from this worktree and the collection was executed for real (see §4.1): **33/33 requests, 101/101 assertions, 0 failures**. `setup.ps1` was intentionally not run; the facility/operations schema volumes were rebuilt, while user/payment/notification/iot/rabbitmq data was left intact. Rows still marked BLOCKED are the ones that need a SignalR client or broker harness.

---

## 0. What is in this branch

| # | Area | Evidence in the worktree | Static status |
|---|---|---|---|
| a | Outbox store | `scripts/database/overlay/realtime-outbox.sql`; `OperationService.Domain/Entities/OutboxEventRow.cs`; `ReservationDbContext` maps `outbox_events` | VERIFIED (static) |
| b | Outbox writes | `Persistence/ReservationGateStore.cs` writes `OccupancyChanged` / `SlotStateChanged` / `OccupancyReleased` in the business transaction | VERIFIED (static) |
| c | Publisher / dispatcher | `Infrastructure/Outbox/Outbox*` + `RabbitMqOutboxPublisher.cs`; envelope and PascalCase→dot.lower routing key | VERIFIED (static) |
| d | Realtime broadcast | `API/Realtime/SignalRLayoutRealtimeBroadcaster.cs`; group `site:{siteId}:operators` | VERIFIED (static) |
| e | Hub | `API/Hubs/ParkingLotLayoutHub.cs`; `/ws/parking-lots/{siteId}/layout`; 401/403 mapping; `SnapshotRequired` | VERIFIED (static) |
| f | Consumer | `FacilityService.Infrastructure/OccupancyProjectionWorker.cs`, `Persistence/OccupancyProjectionStore.cs`; overlay `realtime-projection.sql` | VERIFIED (static) |
| g | Zone-only field | `OperationalLayoutProjection.ZoneCapacityDto.ZoneOnlyReservedCount`; reader approximation | VERIFIED (static) |
| h | OpenAPI delta | `docs/03-design/api/3d-digital-twin-openapi.yaml` — `SlotVisualStateDto` now matches the C# DTO (no per-slot `eventId` / `sequenceNumber` / `siteId`); those live only on `RealtimeEventEnvelope` | VERIFIED (static) — aligned |
| i | FE idempotency guard | `FE/src/lib/realtimeIdempotency.ts` | VERIFIED (static, file exists) |
| j | Tests | no SPARK-244-tagged xUnit/Node suite found in `tests/` | VERIFIED (static) |

---

## 1. Environment

| Component | URL / value | Notes |
|---|---|---|
| UserService | `http://localhost:5035` | login + `api/operators/site-scope` / `slot-scope` |
| OperationService | `http://localhost:5055` | gate/session REST, outbox, hub |
| FacilityService | `http://localhost:5045` | operational-layout, internal gate slots |
| Seeded site | `b0000000-0000-4000-8000-000000000001` (`DEMO-HQ`) | 48 AVAILABLE slots under Zone A |
| Seeded tenant | `a0000000-0000-4000-8000-000000000001` | required by internal claim/release |
| Service key | `.env` `SMARTPARK_SERVICE_KEY` | `X-Service-Key` for `/internal/**`; ≥32 chars |
| Redis | `.env` `SMARTPARK_REDIS_URL` / `Redis:ConnectionString` | only used by SignalR when configured |
| RabbitMQ | `.env` `SMARTPARK_RABBITMQ_*` / `RabbitMq:ConnectionString` | only used by publisher/consumer when configured |

Passwords are never committed. Use `SMARTPARK_OWNER_PASSWORD`, `SMARTPARK_OPERATOR_PASSWORD`, `SMARTPARK_ADMIN_PASSWORD` and `SMARTPARK_SERVICE_KEY` from the repository root `.env`.

---

## 2. Checklist

Status applies to the runtime result. The "Static evidence" column records what was actually reviewed/executed in this task.

| # | WBS / AC | Check | Expected | Static evidence | Runtime status |
|---|---|---|---|---|---|
| C1 | Artifacts | 4 SPARK-244 files present; collection/env JSON parse; every `{{var}}` resolves | 7 folders / 33 requests / 34 env vars; 0 dangling vars; 35 scripts parse | YES (see §4) | **VERIFIED (static + live run)** |
| C2 | P1 infra — Redis | `redis:7-alpine` in compose; `AddSignalR` + conditional `AddStackExchangeRedis` | Backplane enabled only with `Redis:ConnectionString`/`ConnectionStrings:Redis` | `OperationService/Program.cs:26-30`, compose line 111 | **VERIFIED (live, config active)** — `smartpark-redis` healthy and `reservation-service` runs with `Redis__ConnectionString=redis://redis:6379`; multi-instance fan-out remains **BLOCKED** |
| C3 | P2 outbox | `outbox_events` exists with `event_id` PK + unique index + `(site_id,sequence_number)` index | Table/columns match `OutboxEventRow` | `realtime-outbox.sql`, `ReservationDbContext` | **VERIFIED (live)** — table created by the fresh-volume overlay; 16 rows observed |
| C4 | P2 publisher | Poll `published_at IS NULL` with `FOR UPDATE SKIP LOCKED`; publish `{eventId,eventType,occurredAt,sequenceNumber,siteId,payload}`; routing key dot.lower | `OccupancyChanged` → `occupancy.changed`; `published_at` set after publish | `OccupancyOutboxWorker.cs`, `RabbitMqOutboxPublisher.cs` | **VERIFIED (live)** — 16/16 rows published (`published_at` set); `OccupancyChanged=8`, `OccupancyReleased=8` |
| C5 | P2 hub auth | `/ws/parking-lots/{siteId}/layout`; missing/invalid token → `UNAUTHENTICATED`; foreign site → `OPERATOR_LOT_UNASSIGNED`; group `site:{siteId}:operators`; `SnapshotRequired` | Connection rejected with the mapped `HubException`; no events | `ParkingLotLayoutHub.cs:9-40`, `OperatorIdentityClient.cs:21-64` | **BLOCKED** (no WS client driven). NOTE: `POST …/negotiate` returns **200 anonymously** for all probes — the hub has no `[Authorize]`, so rejection can only be observed on the real WebSocket handshake |
| C6 | P2 consumer | Queue `smartpark.facility.occupancy` bound `#`; `processed_events` unique `event_id`; `slot_sequence_watermarks`; release ledger unique `session_id` | Duplicate/stale event → no projection; release once | `OccupancyProjectionWorker.cs`, `OccupancyProjectionStore.cs`, `realtime-projection.sql` | **VERIFIED (live, delivery)** — consumer runs with `RabbitMq__ConnectionString`; `processed_events` = 16 = `outbox_events` total. Duplicate/stale replay still **BLOCKED** |
| C7 | P2 release-once | First release OCCUPIED→AVAILABLE; second release no-op | Second internal release returns 204; slot stays AVAILABLE; ledger has one row/session | `OperatorGateSlotStore.ReleaseAsync`, `ApplyReleaseAsync` | **VERIFIED (live, API layer)** — releases #1 and #2 both 204 and the slot returned to `AVAILABLE`/`UNRESERVED`; consumer ledger **BLOCKED** (walk-in events carry no `slotId`, so the ledger legitimately stays empty) |
| C8 | P2 zone-only | `zoneOnlyReservedCount` present and numeric; slots keep `reservationState` | `zoneOnlyReservedCount` = zone RESERVED count (current approximation) | `OperationalLayoutProjection.cs:43-51`, `PostgresOperationalLayoutReader.cs:143-159` | **VERIFIED (live, shape)** — 4.1/4.2 pass; true zone-only allocation remains **BLOCKED** |
| C9 | P2 OpenAPI delta | `SlotVisualStateDto` carries no per-slot `eventId`/`sequenceNumber`/`siteId`; `RealtimeEventEnvelope` carries them | Envelope (not the snapshot) is the idempotency anchor; DTO matches OpenAPI | OpenAPI `SlotVisualStateDto` aligned to `OperationalLayoutProjection.cs:60-77`; `RealtimeEventEnvelope` unchanged | **VERIFIED (static + live)** — 1.1 asserts the absence against a real response and passes |
| C10 | P3 FE idempotency | Guard keyed by `eventId` + `sequenceNumber` watermark | Replayed delta overwrites the same node, no flicker | `FE/src/lib/realtimeIdempotency.ts` + `operatorLayoutLive.ts` wired into `OperatorDashboard` | **TO RUN** (browser/WS session not executed) |
| C11 | P4 latency | p95 ≤ 2s dev-only target from Operations write to client update | p95 ≤ 2000 ms on ≥20 LAN samples | No automated latency harness | **PARTIAL (live, REST proxy)** — layout read 29–36 ms; write→read 179–204 ms; true WS p95 **BLOCKED** |
| C12 | P4 reconnect | Disconnect → reconnect → full snapshot, no half-state | `SnapshotRequired` + client refetch/replace | Hub sends `SnapshotRequired` (single `siteId` arg) | **BLOCKED** |
| C13 | P4 replay | Same event delivered twice → final state unchanged | `processed_events`/watermark no-op | Store code present | **BLOCKED** (needs broker/DB harness) |
| C14 | P4 scope | Unassigned operator gets 403 `OPERATOR_LOT_UNASSIGNED`; no events | 403 on OperationService REST; hub rejects | `OperatorIdentityClient.cs:41-42` | **VERIFIED (live, REST)** — 5.2 returns 403 `OPERATOR_LOT_UNASSIGNED`; hub rejection **BLOCKED** |
| C15 | Collection run | Full 33-request run against this branch | 0 unexpected failures | Executed with Newman 6.2.3 (see §4.1) | **VERIFIED (live)** — 33/33 requests, 33/33 test-scripts, 101/101 assertions, 0 failures, exit 0 |
| C16 | Scope discipline | Deferred/P2+ work stays deferred | G1 requested-vs-allocated and FE P3 not presented as mandatory MVP passes | This guide | **VERIFIED (static)** |

---

## 3. Detail for the high-risk rows

### C2 — Redis backplane

**Source:** `OperationService/Program.cs:26-30` reads `Redis:ConnectionString` first, then `ConnectionStrings:Redis`; only then calls `AddStackExchangeRedis`. `compose.schema-integration.yml:111-120` defines `redis:7-alpine` with `redis-cli ping`.

**Expected when enabled:** two OperationService instances share hub groups; an event published by instance A reaches a client connected to instance B.

**Status: BLOCKED.** No Redis connection string is configured for the app and no multi-instance run was performed. Unblock by setting `Redis:ConnectionString`, starting two instances, and connecting one client to each.

### C3/C4 — Outbox and publisher

**Static facts:**

- `realtime-outbox.sql` creates `outbox_events(event_id UUID PK, event_type, occurred_at, sequence_number, site_id, payload JSONB, published_at)` with a unique `event_id` index and a partial unpublished index.
- `ReservationGateStore` assigns `SequenceNumber = max(site)+1` per site (not per slot).
- `RabbitMqOutboxPublisher.ToRoutingKey` converts PascalCase to dot.lower (`SlotStateChanged` → `slot.state.changed`).
- The envelope is `{eventId,eventType,occurredAt,sequenceNumber,siteId,payload}`; `payload` is embedded as a JSON object.

**TO RUN:** apply `realtime-outbox.sql`, enable `RabbitMq:ConnectionString`, drive 2.1/2.4, and inspect the row and the RabbitMQ message.

### C5 — Hub auth

**Static facts:** `OnConnectedAsync` takes `siteId` from the route (else `INVALID_SITE_ID`), the token from `?access_token=` or the `Authorization` header (else `UNAUTHENTICATED`), calls `AuthorizeSiteAsync`, maps 401→`UNAUTHENTICATED` and 403→`OPERATOR_LOT_UNASSIGNED`, joins `site:{siteId}:operators`, and sends `SnapshotRequired`.

**BLOCKED:** Postman cannot negotiate a SignalR connection. Use a .NET/JS SignalR client:

```text
connect ws://localhost:5055/ws/parking-lots/<siteId>/layout?access_token=<operator JWT>
expect: SnapshotRequired
connect with a foreign siteId: expect OPERATOR_LOT_UNASSIGNED
connect without a token: expect UNAUTHENTICATED
```

### C6/C7 — Consumer idempotency and release-once

**Static facts:** `OccupancyProjectionStore.ApplyAsync` inserts `processed_events` with `ON CONFLICT (event_id) DO NOTHING`; a zero-row insert rolls back and returns `false`. It then checks `slot_sequence_watermarks` (`last < incoming`), applies `SlotStateChanged`/`OccupancyReleased`, and advances the watermark. `ApplyReleaseAsync` inserts `occupancy_release_ledger` with `ON CONFLICT (session_id) DO NOTHING`; the `UPDATE parking_slots ... FROM ledger` frees the slot only when the ledger insert returned a row.

**Runtime gate:** Facility registers `OccupancyProjectionWorker` only when `RabbitMq:ConnectionString` is set (`FacilityService/Program.cs:46-52`). The session log records the consumer is off by default.

**TO RUN:** enable RabbitMQ for Facility, then drive a reservation check-in/out (needs a seeded reservation; there is no reservation-creation REST route) and run the SQL in `SPARK-244-manual-test-guide.md` §8.2. The true ledger case is **BLOCKED** from Postman.

### C8 — Zone-only reserved count

**Static facts:** `ZoneCapacityDto` exposes `zoneOnlyReservedCount`; the reader computes `reserved = inZone.Count(ReservationState == RESERVED)` and passes `reserved, reserved` for `allocatedSlotCount` and `zoneOnlyReservedCount` (OPEN D0-5). There is no zone-level allocation table or REST route.

**TO RUN:** 4.1/4.2 assert the shape and the approximation. A true zone-only reservation is **BLOCKED** until a product/schema decision adds the source.

### C9 — OpenAPI delta

**Finding (VERIFIED static — RESOLVED):** The OpenAPI previously declared `eventId`, `sequenceNumber`, `siteId` as `SlotVisualStateDto` properties while `OperationalLayoutProjection.cs:60-77` did not include them. That discrepancy has been closed by aligning the document to the code (G5: the envelope, not the snapshot, is the idempotency anchor). The `SlotVisualStateDto` block in `3d-digital-twin-openapi.yaml` is now byte-identical to the baseline main-repo block, and the three fields exist only on `RealtimeEventEnvelope`. The collection asserts their **absence** on slots, which is now the canonical contract.

Also: `RealtimeEventEnvelope` lists `ZoneCapacityChanged` / `CapacityMetricsChanged` / `TelemetryFreshnessChanged`, but no producer writes those event types. `SnapshotRequired` is sent as a single `siteId` argument, not `{siteId, reason}`.

### C10 — FE idempotency

`FE/src/lib/realtimeIdempotency.ts` exists (static). No FE test was executed and no browser/WS run was performed. **TO RUN** the FE guard tests separately; this backend authoring task does not claim FE coverage.

### C11 — Latency target

Q4 is a **dev-only** target, not an SLA. The collection's 6.1/6.3 measure one REST read and one REST write-to-read; they do not measure the hub leg. The true p95 is **BLOCKED** (needs a WS client and ≥20 samples).

---

## 4. Static verification actually executed

These commands/checks were run in this authoring task. They are **not** a runtime pass.

| Check | Command / method | Result |
|---|---|---|
| Environment JSON validity | PowerShell `Get-Content -Raw \| ConvertFrom-Json` | PASS (34 keys) |
| Collection JSON validity | PowerShell `ConvertFrom-Json` | PASS (`schema = .../v2.1.0/collection.json`) |
| Folder/request count | PowerShell traversal | 7 folders, 33 requests (4/6/5/6/2/6/4) |
| Dangling `{{var}}` | Regex over all URL/body/header/description strings vs env keys | 0 dangling; 23 referenced; `seededFloorId`/`seededZoneId` are reference-only |
| `pm.environment.get/set` keys | Regex vs env keys | 0 unknown keys |
| JS syntax | Node `new Function('pm','console', exec.join('\n'))` | 35 scripts parsed, 0 syntax errors |
| OpenAPI vs C# slot DTO | Source read | Mismatch recorded (C9) |

### 4.1 Live run actually executed (supersedes the static-only status)

The collection was executed against a stack **built from this branch**, not the main repo:

| Step | Command / method | Result |
|---|---|---|
| Clean schema rebuild | `docker compose -f compose.schema-integration.yml rm -sf facility-db operations-db` + `docker volume rm smartpark-schema_facility-schema-data smartpark-schema_operations-schema-data` + `up -d facility-db operations-db redis` | Fresh volumes; all four SPARK-244 relations created by the overlays |
| Overlay wiring | `scripts/apply-workflow-migrations.ps1 -Service Parking -SchemaRoot .` and `-Service Operation -SchemaRoot .` | "schema already current; applied incremental features and verified required relations" for both |
| Seed | `scripts/seed-demo-site.ps1` | DEMO-HQ + 2 units + 48 slots; 2 accounts |
| App stack | `docker compose -f compose.app.yml build` + `up -d` | 5 images built from the worktree; user/parking/reservation/payment healthy, frontend up |
| Health | `/health` on 5035/5045/5055 and `/` on 5173 | 200 on all four |
| Logins | `POST /api/auth/login` for owner/operator/admin/driver | 200 with the expected roles |
| **Collection run** | `npx --yes newman run … -e … --env-var ownerPassword=… --env-var operatorPassword=… --env-var adminPassword=… --env-var serviceKey=…` | **33 requests / 33 test-scripts / 101 assertions — 0 failures, exit 0**, 4.8 s, avg response 58 ms |
| Outbox evidence | `SELECT event_type, count(*) FROM outbox_events GROUP BY event_type` | `OccupancyChanged=8`, `OccupancyReleased=8`, total 16, all `published_at` set |
| Consumer evidence | `SELECT count(*) FROM processed_events` (facility DB) | 16 — every published event was consumed exactly once |

**Authoring defect found only by running:** 8 test-scripts declared `const data = …`. `data` is a **Postman/Newman sandbox global**, so every one of them threw `SyntaxError: Identifier 'data' has already been declared` on the first run. The identifier was renamed to `payload` in those 8 scripts (string/comment-aware rename, `body.data` untouched) and the suite then went green. Static parsing could not catch this because each script is valid JavaScript in isolation.

**Note on the hub:** `POST /ws/parking-lots/{siteId}/layout/negotiate?negotiateVersion=1` returns **200 for anonymous, assigned and foreign-site callers alike**. The hub carries no `[Authorize]` and OperationService registers no JWT authentication; the site-scope check lives in `ParkingLotLayoutHub.OnConnectedAsync` and therefore only fires during the real WebSocket connection. Negotiate is not a valid probe for C5/C14-hub.

---

## 5. BLOCKED / not-a-regression

| Item | Exact reason | Unblock |
|---|---|---|
| Hub connect / reconnect / `SnapshotRequired` | Postman has no SignalR client, and `negotiate` is anonymous by design (200 for every caller) | SignalR client (see C5) |
| Live delta delivery (`SlotStateChanged`) | Same; also no reservation-creation REST route | Seed a reservation + WS client |
| True duplicate-event replay | Needs broker/DB harness, not Postman | Publish the same envelope twice; query `processed_events`/watermark |
| Consumer release-once **ledger path** | The consumer now runs live and consumed 16/16 events, but every walk-in event carries `slotId=null`, so `occupancy_release_ledger` and `slot_sequence_watermarks` are legitimately never written | Seed a reservation with a slot, check it in/out, then run the §8.2 SQL |
| True zone-only reservation | No zone-level allocation source (OPEN D0-5) | Product/schema decision |
| p95 ≤ 2s | Needs ≥20 WS samples across the full pipeline | WS client harness |
| Redis multi-instance fan-out | Redis is running and the backplane is configured, but only one OperationService instance exists | Second instance + one client each |
| G1 requested vs allocated (A7) | Not implemented in the projection (`OperatorContext` always null) | P3 FE + a requested/allocated source; do not mark as an MVP pass |
| FE idempotency guard | FE is now wired (`operatorLayoutLive.ts` → dashboard refresh) but no browser/WS session was run | Run the manual browser case |

**Do not upgrade deferred/P2+ scope into mandatory MVP items.** G1 requested-vs-allocated, FE 3D rendering, overstay penalties, and production SLA remain deferred follow-ups per `docs/04-testing/README.md`. The absence of these features is recorded as BLOCKED/not-implemented, not as a SPARK-244 regression to "fix" in this branch.

---

## 6. Suggested live run order

1. **[DONE]** Parse the artifacts (C1).
2. **[DONE — different path]** `setup.ps1` was intentionally **not** run (branch constraint). Instead: worktree `.env`, surgical schema rebuild of `facility-db` + `operations-db`, `up -d redis`, `./scripts/seed-demo-site.ps1`, then `docker compose -f compose.app.yml build` + `up -d` from the worktree.
3. **[DONE]** Overlays are wired into `apply-workflow-migrations.ps1` and were re-run as an idempotency check (`-Service Parking`, `-Service Operation`); a fresh volume applies them automatically.
4. **[DONE]** Newman run 00 → 06: 33/33 requests, 101/101 assertions, 0 failures.
5. **[DONE]** Facility consumer runs with `RabbitMq__ConnectionString`; 16/16 outbox events reached `processed_events`. The §8.2 ledger/watermark rows are only written for slot-bearing events, so they stay empty for walk-in traffic.
6. **[OPEN]** Redis backplane is configured and healthy, but two OperationService instances were not run.
7. **[OPEN]** SignalR client cases: C5, C12, C13, C14-hub, C11.
8. **[DONE]** Actual numbers recorded above; p95 and hub behaviour are still not claimed from the REST proxies.

---

## 7. Source evidence

| Topic | Path |
|---|---|
| Hub / Redis / broadcaster / CORS | `src/Services/OperationService/SmartParking.OperationService.API/Program.cs` |
| Hub auth mapping | `.../API/Hubs/ParkingLotLayoutHub.cs`, `.../Infrastructure/Clients/OperatorIdentityClient.cs` |
| Outbox row/publisher/worker | `.../Domain/Entities/OutboxEventRow.cs`, `.../Infrastructure/Outbox/*`, `.../Infrastructure/InfrastructureDependencyInjection.cs` |
| Outbox writes / sequence | `.../Persistence/ReservationGateStore.cs` |
| Broadcast envelope | `.../API/Realtime/SignalRLayoutRealtimeBroadcaster.cs` |
| Consumer / idempotency | `FacilityService/Infrastructure/OccupancyProjectionWorker.cs`, `FacilityService/Persistence/OccupancyProjectionStore.cs` |
| Snapshot DTO / zone field | `FacilityService/Application/OperationalLayoutProjection.cs` |
| Snapshot reader | `FacilityService/Persistence/PostgresOperationalLayoutReader.cs` |
| Internal claim/release | `FacilityService/API/Program.cs`, `FacilityService/Persistence/OperatorGateSlotStore.cs` |
| Scope routes | `UserService/API/Controllers/OperatorsController.cs`, `UserService/Application/Usecase/IdentityAccess/OperatorSlotScope.cs` |
| SQL | `scripts/database/overlay/realtime-outbox.sql`, `realtime-projection.sql` |
| Design | `docs/03-design/api/3d-digital-twin-spark-244-delta-design.md`, `3d-digital-twin-openapi.yaml` |