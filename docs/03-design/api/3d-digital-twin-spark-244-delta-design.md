# SmartPark — 3D Digital Twin API Design: SPARK-244 Delta Design Pass (P0)

| Metadata | Value |
| :--- | :--- |
| **Document ID** | `SPARK-API-3D-244-DELTA-P0` |
| **Status** | **P0 DELTA DESIGN — G1–G6 CLOSED (design only)** |
| **Contract Version** | `v1.2.0` (delta applied at design level; implementation pending SPARK-228 merge) |
| **Scope** | SPARK-244 P0 — Sprint 3 operator monitoring vertical slice (US-O03) |
| **Date** | 2026-10-28 |
| **Owning Service** | `ParkingService` (Target: `Facility & Visualization Service`) |
| **Companion** | `3d-digital-twin-spark-244-api-design.md`, `3d-digital-twin-api-design.md` (hub) |

> This document is the **P0 delta design pass** output. It closes the 6 gaps (G1–G6) identified in `3d-digital-twin-spark-244-api-design.md §1` and `§3.2`. It is **design only**: no code, no DB migration, no compose change. Schema proposals are indicative; final SQL lands in the implementation PR after SPARK-228 merge.

---

## 0. Decisions carried from planning (Q1–Q4)

| # | Decision | Status |
| :--- | :--- | :--- |
| Q1 | Redis as SignalR backplane | ACCEPTED — propose Redis service in compose (not yet added) |
| Q2 | Operations is the source of truth for occupancy | ACCEPTED — realtime channel only republishes Operations-chosen state |
| Q3 | Event identity: dedicated `event_id` in outbox; unique on `event_id` | ACCEPTED |
| Q4 | p95 ≤ 2s is a **dev-environment target only**, not an SLA | ACCEPTED |

---

## 1. Gap closure summary

| Gap | Title | Status | Closing section |
| :--- | :--- | :--- | :--- |
| G1 | Requested vs allocated have no separate fields | **CLOSED** | §2 |
| G2 | Operations not asserted as occupancy authority in realtime contract | **CLOSED** | §3 |
| G3 | Release-once undefined | **CLOSED** | §4 |
| G4 | Zone-only precision has no enforcement rule | **CLOSED** | §5 |
| G5 | Duplicate/replayed event has no rule | **CLOSED** | §6 |
| G6 | Assigned scope not checked on realtime channel | **CLOSED** | §7 |

---

## 2. G1 — Requested vs allocated (CLOSED)

**Problem:** layout DTO exposes a single location per slot; ticket requires requested reservation location and actual allocated location displayed distinctly.

**Decision (design):** The existing `operatorContext.approachingReservation` (already in §4.4 of 244 design) is the canonical surface and is **extended, not duplicated**:

```json
"approachingReservation": {
  "reservationId": "uuid",
  "requestedSlotCode": "PARKING_G_CAR_002",
  "allocatedSlotCode": "PARKING_G_CAR_001",
  "isReallocated": true,
  "reallocationReason": "OCCUPIED_OVERSTAY_DISPLACEMENT",
  "expectedArrivalTime": "ISO8601",
  "gracePeriodExpiresAt": "ISO8601"
}
```

**Rules:**
- `requestedSlotCode` = slot code the driver originally requested (immutable for the reservation lifecycle).
- `allocatedSlotCode` = slot code Operations actually allocated (may equal requested; may change on reallocation).
- `isReallocated = (requestedSlotCode != allocatedSlotCode)`.
- When `allocatedSlotCode == null` (zone-only / not yet allocated), the reservation is **zone-only** (see G4) and no slot-level halo is rendered.
- The REST snapshot (`operational-layout`) and the SignalR delta both carry this payload identically (single source of shape).
- UI must render requested and allocated as two distinct chips/badges; never collapse into one.

**Delta to OpenAPI:** add `approachingReservation` to `OperationalLayoutProjectionDto.slots[].operatorContext` (already present in current contract v1.2.0 — confirm no rename).

---

## 3. G2 — Operations as occupancy authority (CLOSED)

**Problem:** "accepted check-in changes projection to occupied only from authoritative Operations state" not bound to a named service/event.

**Decision (design):**
- **Source of truth:** `ReservationService` Operations domain (sessions, allocations, gate events). `ParkingService` is a **projection consumer**, never an authority.
- `ParkingService` MUST NOT derive occupancy from IoT telemetry alone, from 3D client input, or from payment events.
- Occupancy transitions flow:
  1. Gate/sensor → `ReservationService` (accepted check-in `POST /internal/gate/check-in`).
  2. `ReservationService` writes session row (`session_status = ACTIVE`, `checked_in_at`).
  3. `ReservationService` publishes domain event via outbox: `OccupancyChanged { slotId, physicalState: OCCUPIED, sessionId, occurredAt, eventId }`.
  4. `ParkingService` consumes event, updates projection, broadcasts SignalR delta.
- `ParkingService` may still serve `operational-layout` snapshot by joining its own structural data with the latest Operations-published occupancy (cached projection), but the **transition trigger** is always the Operations event.

**Invariant:** No code path in `ParkingService` may set `physicalState = OCCUPIED` except in the event consumer handler for `OccupancyChanged(physicalState=OCCUPIED)`.

---

## 4. G3 — Release-once semantics (CLOSED)

**Problem:** "authoritative actual departure changes projection to released exactly once" has no event identity/idempotency rule.

**Decision (design):**
- Departure event: `OccupancyReleased { slotId, sessionId, occurredAt, eventId }` published by `ReservationService` when exit gate barrier confirms physical departure (`POST /internal/gate/check-out`).
- **Release-once rule:** A given `sessionId` may be released at most once. The consumer (ParkingService) maintains an idempotency ledger keyed by `eventId` (see §6) AND by `sessionId`:
  - If `eventId` already processed → no-op (G5).
  - If `sessionId` already released (even under a different `eventId`, e.g. replay with new id) → no-op; log `RELEASE_ALREADY_APPLIED`.
- On first successful release: set `physicalState = AVAILABLE`, clear `activeSessionId`, set `updatedAt = occurredAt`. Increment available capacity metrics.
- **Payment alone never releases.** `POST /api/payments` (checkout settlement) updates financial ledger only; no `OccupancyReleased` event is emitted. Slot stays `OCCUPIED` until exit gate event.
- **Idempotency ledger** (proposed, no SQL yet): table `occupancy_release_ledger (event_id UUID PK, session_id UUID, slot_id UUID, released_at TIMESTAMPTZ, processed_at TIMESTAMPTZ)`. Unique on `event_id`; secondary unique on `session_id` (release-once).

---

## 5. G4 — Zone-only precision (CLOSED)

**Problem:** zone-only/area-only location must never be rendered as invented exact slot; projection has no zone-level representation.

**Decision (design):**
- When a reservation is allocated to a **zone** (no specific slot): `target_slot_id IS NULL`, `target_unit_id` is a ZONE spatial unit.
- Such reservations increment `metrics.zoneCapacities[].zoneOnlyReservedCount` (already in DTO).
- All individual `slots[]` in that zone keep `reservationState = UNRESERVED` unless individually allocated.
- **Hard UI constraint:** 3D renderer highlights the zone perimeter/badge; MUST NOT place a proxy vehicle or glowing halo on any unallocated stall mesh. No synthetic `slot-auto-*`.
- Realtime delta for zone-only reservations carries `zoneCode` + `zoneOnlyDelta` (±1) instead of a `slotId`. Consumers update the zone badge count, never a slot node.
- **No "upgrading":** if only zone precision is recorded, the system must not infer or assign an exact slot for display purposes (BR-EMERG-01 / FR-MAP-03).

---

## 6. G5 — Duplicate / replayed events (CLOSED)

**Problem:** no `eventId` or sequence number in event envelope → duplicate suppression impossible.

**Decision (design):**

**Event envelope (all ParkingService-consumed events):**
```json
{
  "eventId": "uuid (outbox PK, unique)",
  "eventType": "OccupancyChanged | OccupancyReleased | AllocationChanged | ZoneCapacityChanged",
  "occurredAt": "ISO8601 UTC",
  "sequenceNumber": "int64 (monotonic per slotId, assigned by publisher)",
  "siteId": "uuid",
  "payload": { ... }
}
```

**Idempotency rules (consumer side):**
1. Maintain `processed_events (event_id UUID PK, processed_at TIMESTAMPTZ)`. Unique on `event_id`.
2. On receive: if `eventId` exists → no-op, ACK, do not re-apply (log `DUPLICATE_EVENT_IGNORED`).
3. If `sequenceNumber` < last applied sequence for that `slotId` → treat as stale/replay → no-op (log `STALE_SEQUENCE_IGNORED`). Do not move state backward.
4. If `sequenceNumber` == last applied → no-op (duplicate).
5. If `sequenceNumber` > last applied → apply, update last sequence.
6. **Outbox side (ReservationService):** table `outbox_events (event_id UUID PK, aggregate_id UUID, event_type TEXT, payload JSONB, occurred_at TIMESTAMPTZ, published_at TIMESTAMPTZ NULL)`. Unique on `event_id`. Publisher uses MassTransit outbox pattern; events are written in the same transaction as the business state change.
7. **SignalR client side:** key updates by `slotId` (or `zoneCode` for zone-only). Replayed messages overwrite the same node idempotently; no duplicate visual side-effects (no flicker, no double animation).

**Q3 (event identity):** dedicated `event_id` in outbox (not business id), because business id (`allocation_id`) may be reused across slot changes.

---

## 7. G6 — Assigned scope on realtime channel (CLOSED)

**Problem:** operator access enforced per site via `SlotOperatorClient.ScopeAsync()`; "view only lots within current assigned scope" needs explicit mapping onto realtime channel.

**Decision (design):**
- **Connection authorization:** SignalR hub `/ws/parking-lots/{siteId}/layout` requires JWT Bearer. On connect:
  - Validate JWT (401 `UNAUTHENTICATED` if invalid/expired — never degrade to anonymous).
  - Resolve `AssignedLots` claim from token (list of site UUIDs the operator is assigned to).
  - If `siteId ∉ AssignedLots` AND role lacks `SLOT_OVERRIDE` → reject connection with `403 OPERATOR_LOT_UNASSIGNED` (hub `OnConnectedAsync` throws `HubException` → client receives error, no events).
- **Group scoping:** server adds connection to group `site:{siteId}:operators`. Events are published only to that group. An operator connected to site A never receives events for site B.
- **REST parity:** `GET /operational-layout` for operator without assignment returns `403 OPERATOR_LOT_UNASSIGNED` (TC-244-01). Same rule, same code.
- **Token refresh:** on reconnect, client re-sends JWT; server re-evaluates scope. Scope changes (admin reassigns lots) take effect on next connect/reconnect; no mid-connection scope push required.
- **Audit:** log connection attempts with `userId`, `siteId`, `result (allowed/denied)`.

---

## 8. SignalR hub contract (proposed, design only)

- **Hub path:** `/ws/parking-lots/{siteId}/layout` (DEC-3D-02 Option B).
- **Transport:** SignalR over WebSocket (fallback long-polling).
- **Auth:** JWT Bearer via query string `?access_token=` (browser WebSocket) or Authorization header (native clients).
- **Server → client events:**
  - `SlotStateChanged { eventId, sequenceNumber, siteId, slotId, slotCode, physicalState, reservationState, activeSessionId, updatedAt, approachingReservation }`
  - `ZoneCapacityChanged { eventId, siteId, zoneCode, zoneOnlyReservedCount, allocatedSlotCount }`
  - `CapacityMetricsChanged { eventId, siteId, occupiedCount, reservedCount, availableCarSlots }`
  - `TelemetryFreshnessChanged { siteId, isFresh, generatedAt }`
  - `SnapshotRequired { siteId, reason: "reconnect" }` (server hint; client fetches REST snapshot)
- **Client → server:** none (read-only; no mutation authority — AC-10).
- **On connect:** server sends `SnapshotRequired` (or pushes full snapshot) so client loads authoritative state. No delta replay from server memory; client fetches `GET /operational-layout`.
- **Backplane:** Redis (Q1). Required when running >1 instance. Compose proposal in §9.

---

## 9. Infrastructure proposals (design only — no compose edit yet)

### 9.1 Redis backplane (Q1)
- Add Redis 7 service to `compose.schema-integration.yml`:
  - image `redis:7-alpine`, port 6379, healthcheck `redis-cli ping`.
  - Used by both telemetry heartbeat (§4.5 of 244 design) and SignalR backplane.
- Env vars: `REDIS_CONNECTION_STRING`, `SIGNALR_REDIS_BACKPLANE_ENABLED=true`.
- `ParkingService` adds `Microsoft.AspNetCore.SignalR.StackExchangeRedis` + `StackExchange.Redis`.
- **Note:** this is 244 infrastructure, not 228. 228 remains single-instance prototype; backplane lands in P1 of 244.

### 9.2 MassTransit + outbox (ReservationService)
- Add `MassTransit` + `MassTransit.EntityFrameworkCore` (or RabbitMQ transport) to `ReservationService`.
- Outbox table `outbox_events` (proposed schema §6.5) in ReservationService DB.
- Publisher: transactional outbox — event written in same transaction as business state change; background dispatcher publishes to RabbitMQ.
- Consumer: `ParkingService` subscribes to `OccupancyChanged`, `OccupancyReleased`, `AllocationChanged`, `ZoneCapacityChanged`.

### 9.3 Proposed outbox schema (indicative, no SQL yet)
```
outbox_events (
  event_id UUID PRIMARY KEY,
  aggregate_type TEXT NOT NULL,        -- 'slot' | 'zone' | 'site'
  aggregate_id UUID NOT NULL,
  event_type TEXT NOT NULL,
  payload JSONB NOT NULL,
  occurred_at TIMESTAMPTZ NOT NULL,
  published_at TIMESTAMPTZ NULL,
  retry_count INT NOT NULL DEFAULT 0
)
CREATE UNIQUE INDEX ux_outbox_event_id ON outbox_events(event_id);
```

Consumer-side:
```
processed_events (
  event_id UUID PRIMARY KEY,
  event_type TEXT NOT NULL,
  processed_at TIMESTAMPTZ NOT NULL
)
```
```
slot_sequence_watermark (
  slot_id UUID PRIMARY KEY,
  last_sequence_number BIGINT NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL
)
```

---

## 10. Event flow mapping (Operations → projection)

```
[Gate/Sensor] --check-in--> ReservationService
   ReservationService: write session (ACTIVE), write outbox event OccupancyChanged (same tx)
   outbox dispatcher --> RabbitMQ --> ParkingService consumer
   ParkingService consumer: idempotency check (eventId, sequence) --> update projection
   ParkingService: broadcast SignalR SlotStateChanged to group site:{siteId}:operators

[Payment] --> ReservationService: settle payment (financial only)  -- NO occupancy event --

[Exit gate] --check-out--> ReservationService
   ReservationService: close session, write outbox OccupancyReleased (same tx)
   ... --> ParkingService consumer: release-once check --> set AVAILABLE --> broadcast
```

**Invariant:** payment never emits occupancy event. Only exit-gate departure emits `OccupancyReleased`.

---

## 11. Reconnect protocol

1. Client detects disconnect → mark state `STALE` (reuse 228 stale banner: 30% dim + banner).
2. On reconnect: SignalR `OnConnected` → server sends `SnapshotRequired`.
3. Client fetches `GET /operational-layout` (full snapshot, atomic) → replaces entire projection (no partial merge).
4. Client resumes delta processing from new snapshot's `generatedAt` / sequence watermarks.
5. Duplicate deltas arriving during reconnect are dropped by idempotency ledger.
6. **No "half state":** snapshot replaces, never merges.

---

## 12. p95 latency target (Q4 — dev only)

- Target: p95 ≤ 2s from Operations state change to client projection update, measured on LAN dev.
- **Not an SLA, not in contract, not in OpenAPI.** Test reports actual measured value (TC-244-06 / 4.6).
- Production SLA deferred to a later release; do not quote 2s in customer-facing docs.

---

## 13. Dependencies & blockers

| Dependency | Status | Impact on 244 |
| :--- | :--- | :--- |
| SPARK-228 (3D model BE, endpoint, freshness, state model) | **MERGED** (origin/develop ea0d212c) | Unblocks P2 envelope/freshness semantics |
| SPARK-238 (structure read/write) | In progress | Tombstone handling: 244 must drop deleted slots from operational view instead of re-showing |
| Redis infra | Not yet in compose | P1 adds service |
| MassTransit | Not in any .csproj | P1 adds package |
| SignalR | Not in any .csproj | P2 adds hub |

**Blocker remaining:** none for P0 (design). P1/P2 require SPARK-228 merged (done) + this delta approved.

---

## 14. Gaps still open (none from G1–G6)

All 6 gaps closed at design level. Open items that are **not** gaps but follow-ups:
- OpenAPI delta: add SignalR event schemas + `OPERATOR_LOT_UNASSIGNED` error code (P5).
- Compose Redis service (P1, not P0).
- Outbox SQL (P2, after 228).
- FE idempotency ledger implementation (P3).

---

## 15. Traceability (AC → gap → test)

| AC | Gap | Test |
| :--- | :--- | :--- |
| AC-1 (assigned scope) | G6 | TC-244-01 |
| AC-2 (requested vs allocated) | G1 | TC-244-02 |
| AC-4 (check-in → occupied from Operations) | G2 | TC-244-03 |
| AC-5 (checkout/payment does not free) | G3 | TC-244-05 |
| AC-6 (departure releases exactly once) | G3 | TC-244-04 |
| AC-7 (zone-only no invented slot) | G4 | TC-244-07 |
| AC-8 (stale/reconnect) | — (228) | TC-244-06 |
| AC-9 (duplicate/replay no duplicate effect) | G5 | TC-244-08 |
| AC-10 (no client mutation authority) | — | add during delta (gap noted in 244 doc §12) |
