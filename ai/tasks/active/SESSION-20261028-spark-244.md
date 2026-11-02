# Session Log — SPARK-244

## 1. Mục tiêu & phạm vi
US-O03 Operator — Monitor assigned parking lots and current operations.
Sprint 3 vertical slice: realtime operational view với backend-confirmed state.

## 2. Đã hoàn thành
### P0 — Delta design (G1–G6 CLOSED)
- docs/03-design/api/3d-digital-twin-spark-244-delta-design.md
- G1: requestedSlotCode / allocatedSlotCode tách biệt
- G2: Operations là source of truth; ParkingService chỉ consumer projection
- G3: Release-once qua OccupancyReleased + idempotency ledger keyed eventId/sessionId; payment không release
- G4: Zone-only → zoneOnlyReservedCount, slots giữ UNRESERVED
- G5: Envelope eventId + sequenceNumber, outbox + processed_events + watermark
- G6: Hub reject 403 OPERATOR_LOT_UNASSIGNED nếu siteId ∉ AssignedLots

### P1 — Hạ tầng
- compose.schema-integration.yml: thêm redis:7-alpine (port SMARTPARK_REDIS_PORT, healthcheck redis-cli ping)
- .env.example: thêm SMARTPARK_REDIS_URL
- scripts/database/overlay/realtime-outbox.sql: outbox_events, processed_events, slot_sequence_watermark, occupancy_release_ledger (unique event_id + session_id)
- Chưa chạm apply-workflow-migrations.ps1 (merge checklist)

### P3 — FE idempotency
- FE/src/lib/realtimeIdempotency.ts: guard decide/commit/reset theo envelope eventId + sequenceNumber watermark

### P5 — OpenAPI delta
- 3d-digital-twin-openapi.yaml: eventId/sequenceNumber/siteId trong SlotVisualStateDto, schema RealtimeEventEnvelope, ví dụ lỗi OPERATOR_LOT_UNASSIGNED

### P2 (phần OperationService — producer side) — DONE
- Domain/Entities/OutboxEventRow.cs: outbox_events row (eventId, eventType, occurredAt, sequenceNumber, siteId, payload, publishedAt)
- Persistence/ReservationDbContext.cs: DbSet<OutboxEventRow> OutboxEvents + mapping outbox_events
- API/Hubs/ParkingLotLayoutHub.cs: JWT claim AssignedLots scope check (OPERATOR_LOT_UNASSIGNED), group site:{siteId}:operators, SnapshotRequired on connect
- Application/Common/Interfaces/Services/IOutboxEventPublisher.cs: publish contract + OutboxMessage record
- Persistence/ReservationGateStore.cs: NextSequenceAsync(siteId) + outbox emit trong 4 mutation paths (walk-in check-in → OccupancyChanged, walk-in check-out → OccupancyReleased, reservation check-in → SlotStateChanged, reservation check-out → OccupancyReleased); ghi cùng transaction, không publish trong tx
- Infrastructure/Outbox/RabbitMqOutboxWorker.cs + RabbitMqOutboxPublisher.cs: poll published_at IS NULL (FOR UPDATE SKIP LOCKED, batch 100), publish envelope, set published_at; backoff 1s→60s
- Infrastructure/InfrastructureDependencyInjection.cs: đăng ký publisher/worker khi có RabbitMq connection string, fallback DisabledOutboxEventPublisher
- Build xanh sau mỗi bước: `dotnet build SmartParking.slnx` 0 errors, 0 warnings

## 3. Chưa làm
- P2 FacilityService projection (consumer side) — deferred, cần design decision
- P4: Test (reconnect, replay, scope, zone-only, release-once, latency)

## 4. Quyết định đã chốt
- Q1: Redis backplane cho SignalR (ACCEPTED)
- Q2: Operations là nguồn sự thật occupancy (ACCEPTED)
- Q3: event_id riêng trong outbox, unique index (ACCEPTED)
- Q4: p95 ≤ 2s chỉ mục tiêu dev, không SLA (ACCEPTED)

## 5. Blockers còn mở
- apply-workflow-migrations.ps1 chưa thêm overlay realtime-outbox.sql (merge checklist)
- smartpark-app-reservation-service-1 chạy pre-change binary (không đụng)
- 56 pre-existing FE harness failures, 3 pre-existing TS errors (không fix)

## 6. Trạng thái hiện tại
P0–P1–P3–P5 done. P2 OperationService (producer side) done. P2 FacilityService (consumer side) deferred.

## 7. Next step
P2 FacilityService projection (consumer side) — sau khi SPARK-238 merge; cần design decision cho cross-service data flow (Operations → FacilityService qua outbox/RabbitMQ consumer).
