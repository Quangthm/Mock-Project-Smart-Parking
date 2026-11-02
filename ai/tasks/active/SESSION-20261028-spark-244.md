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
- scripts/database/overlay/realtime-outbox.sql (operations-db) + realtime-projection.sql (facility-db): outbox_events, processed_events, slot_sequence_watermarks, occupancy_release_ledger (unique event_id + session_id) — tách 2 file theo DB, khớp entity
- Chưa chạm apply-workflow-migrations.ps1 (merge checklist)

### P3 — FE idempotency
- FE/src/lib/realtimeIdempotency.ts: guard decide/commit/reset theo envelope eventId + sequenceNumber watermark

### P5 — OpenAPI delta
- 3d-digital-twin-openapi.yaml: eventId/sequenceNumber/siteId trong SlotVisualStateDto, schema RealtimeEventEnvelope, ví dụ lỗi OPERATOR_LOT_UNASSIGNED

### P2 (phần OperationService — producer side) — DONE
- Domain/Entities/OutboxEventRow.cs: outbox_events row (eventId, eventType, occurredAt, sequenceNumber, siteId, payload, publishedAt)
- Persistence/ReservationDbContext.cs: DbSet<OutboxEventRow> OutboxEvents + mapping outbox_events
- API/Hubs/ParkingLotLayoutHub.cs: xác thực scope qua IOperatorIdentityClient.AuthorizeSiteAsync (uỷ quyền cho UserService; repo pattern), 401→UNAUTHENTICATED, 403→OPERATOR_LOT_UNASSIGNED; group site:{siteId}:operators, SnapshotRequired on connect
- Application/Common/Interfaces/Services/IOutboxEventPublisher.cs: publish contract + OutboxMessage record
- Persistence/ReservationGateStore.cs: NextSequenceAsync(siteId) + outbox emit trong 4 mutation paths (walk-in check-in → OccupancyChanged, walk-in check-out → OccupancyReleased, reservation check-in → SlotStateChanged, reservation check-out → OccupancyReleased); ghi cùng transaction, không publish trong tx
- Infrastructure/Outbox/OccupancyOutboxWorker.cs + RabbitMqOutboxPublisher.cs: poll published_at IS NULL (FOR UPDATE SKIP LOCKED, batch 100), publish envelope, set published_at; backoff 1s→60s
- Infrastructure/InfrastructureDependencyInjection.cs: đăng ký publisher/worker khi có RabbitMq connection string, fallback DisabledOutboxEventPublisher
- Build xanh sau mỗi bước: `dotnet build SmartParking.slnx` 0 errors, 0 warnings

### P2 (phần FacilityService — consumer side) — DONE
- scripts/database/overlay/realtime-outbox.sql: tách lại chỉ cho operations-db (outbox_events: event_id PK, event_type, occurred_at, sequence_number, site_id, payload JSONB, published_at NULL + index unpublished partial + index (site_id, sequence_number)); khớp entity OutboxEventRow
- scripts/database/overlay/realtime-projection.sql (facility-db): processed_events (event_id PK), slot_sequence_watermarks (slot_id PK), occupancy_release_ledger (event_id PK + unique session_id)
- compose.schema-integration.yml: mount realtime-projection.sql vào facility-db (08-realtime-projection.sql), realtime-outbox.sql vào operations-db (06-overlay-realtime-outbox.sql)
- OperationService: IRealtimeLayoutBroadcaster (Application) + SignalRLayoutRealtimeBroadcaster (API, IHubContext<ParkingLotLayoutHub>) — broadcast envelope {eventId, eventType, occurredAt, sequenceNumber, siteId, payload} vào group site:{siteId}:operators sau khi publish outbox; broadcast fail không chặn outbox
- OccupancyOutboxWorker: bỏ query trùng, mở transaction trước, FOR UPDATE SKIP LOCKED batch, publish + broadcast + set published_at trong 1 pass
- FacilityService — tách layer đúng chuẩn (xem P2.1):
  - Application/OccupancyProjectionStoreContracts.cs: port IOccupancyProjectionStore.ApplyAsync(OccupancyProjectionEvent, ct) + record OccupancyProjectionEvent (transport-neutral: eventId/eventType/occurredAt/sequenceNumber/siteId/slotId?/sessionId?/physicalState?)
  - Persistence/OccupancyProjectionStore.cs: raw Npgsql theo style FacilityService (ctor NpgsqlDataSource)
    - G5: processed_events ON CONFLICT(event_id) DO NOTHING (dup → rollback, trả false); watermark slot_id FOR UPDATE, chỉ apply khi sequence_number > last; SlotStateChanged → UPDATE parking_slots.physical_state/reservation_state WHERE site_id + deleted_at IS NULL; OccupancyReleased → INSERT occupancy_release_ledger ON CONFLICT(session_id) DO NOTHING, chỉ khi insert thành công mới UPDATE parking_slots SET physical_state='AVAILABLE' WHERE physical_state='OCCUPIED' (release-once G3); walk-in/không slot → chỉ ghi processed
  - Infrastructure/OccupancyProjectionWorker.cs: chỉ còn adapter RabbitMQ.Client 7.1.2 (queue smartpark.facility.occupancy bind exchange smartpark.occupancy routing #, manual ack, prefetch 16, auto-reconnect 5s); parse envelope → OccupancyProjectionEvent; resolve IOccupancyProjectionStore qua IServiceScopeFactory; ack sau commit, lỗi → nack requeue
- FacilityService.API Program.cs: AddScoped<IOccupancyProjectionStore, OccupancyProjectionStore>() + đăng ký OccupancyProjectionWorker chỉ khi có RabbitMq:ConnectionString
- csproj: FacilityService.Infrastructure chỉ còn RabbitMQ.Client 7.1.2 (Npgsql giữ nguyên ở Persistence)
- Build xanh: FacilityService.API 0 errors 0 warnings; OperationService.API 0 errors (2 NU1902 MessagePack pre-existing)

### P2.1 — Rà soát tuân thủ coding rule/skill (DONE)
- [HIGH] Đã sửa — FacilityService consumer vi phạm phân lớp (SQL/Db trong Infrastructure): chuyển sang Application port + Persistence store; Infrastructure chỉ còn transport adapter; gỡ package Npgsql khỏi Infrastructure.csproj
- [HIGH] Đã sửa — OperationService appsettings.json/.Development.json (mới tạo) chứa Jwt Issuer/Audience không dùng (hub uỷ quyền auth cho UserService; service không đăng ký AddAuthentication/AddJwtBearer) → xoá 2 file
- [LOW] Đã sửa — InfrastructureDependencyInjection.cs: gỡ AddSingleton<ILoggerFactory, LoggerFactory>() thừa
- Tuân thủ tốt (không đổi): hub fail-closed uỷ quyền UserService (skill §4); release-once bằng unique constraint + conditional UPDATE (§5); outbox staging cùng DbContext (§8); overlay SQL không tự apply (§9)
- Defer sang P4: race cấp sequence producer (NextSequenceAsync = Max+1/site; index (site_id, sequence_number) không unique → có thể trùng khi check-in đồng thời cùng site)

### P4 — Test artifacts (Postman) + chạy LIVE — DONE (green)
- docs/04-testing/postman/SmartParking-Realtime-Layout-SPARK-244.postman_collection.json (7 folders / 33 requests)
- docs/04-testing/postman/SmartParking-Realtime-Layout-SPARK-244.postman_environment.json (34 vars)
- docs/04-testing/postman/SPARK-244-manual-test-guide.md (run-book + case→acceptance map + "What was NOT verified")
- docs/04-testing/test-plans/SPARK-244-branch-verification-test-guide.md (WBS checklist VERIFIED/TO RUN/BLOCKED)
- Phạm vi verify (theo quyết định): viết artifact + verify tĩnh/đối chiếu source; KHÔNG đổi môi trường, KHÔNG chạy live. Stack đang chạy là main-repo develop (không có thay đổi SPARK-244: không hub, không Redis, không consumer, user-service down) → case live-only đánh dấu BLOCKED.
- Verify tĩnh tự chạy: JSON hợp lệ (collection + environment), 7 folders/33 requests, 23 biến {{...}} / 0 thiếu, script JS 0 lỗi cú pháp.
- Đối chiếu source (xác nhận): AuthorizeSiteAsync 401 UNAUTHENTICATED / 403 OPERATOR_LOT_UNASSIGNED / 503 IDENTITY_UNAVAILABLE; claim → physical_state='OCCUPIED' + reservation_state=NULL; release → 'AVAILABLE' chỉ khi 'OCCUPIED'; operational-layout contract (metrics/zones/slots); zoneOnlyReservedCount; walk-in VEHICLE_ALREADY_INSIDE; internal claim/release.
- PHÁT HIỆN lệch doc↔code: C# SlotVisualStateDto (OperationalLayoutProjection.cs:60) KHÔNG có eventId/sequenceNumber/siteId, nhưng 3d-digital-twin-openapi.yaml (P5) khai báo có → collection assert "absence" và ghi discrepancy. ĐÃ CHỐT + SỬA: gỡ 3 field khỏi OpenAPI (đúng G5: envelope mang eventId/sequenceNumber), giữ nguyên trên RealtimeEventEnvelope. Khi sửa phát hiện block SlotVisualStateDto trong openapi đã bị thụt lề sai (4 spaces, thành sibling của schema) do lần edit P5 → đã khôi phục đúng 8 spaces và hiện BYTE-IDENTICAL với block gốc ở repo chính (develop). Đã cập nhật lại 2 guide: discrepancy #1 = RESOLVED/canonical contract.
- Lệch khác ghi trong guide: operatorContext.approachingReservation (G1) chưa impl (reader luôn null); G4 allocatedSlotCount == zoneOnlyReservedCount (xấp xỉ, OPEN D0-5); G6 REST parity false (route trả 200 sanitized, 403 do OperationService identity client); outbox SQL bỏ aggregate_type/aggregate_id/retry_count; hub SnapshotRequired gửi 1 arg siteId.

### P4 live (B1 clean rebuild) — DONE, suite xanh
- Môi trường: copy `.env` từ repo chính sang worktree + thêm `SMARTPARK_REDIS_PORT=6379`, `SMARTPARK_REDIS_CONNECTION=redis://redis:6379`. Dọn có chọn lọc: `docker compose -f compose.app.yml down`; `rm -sf facility-db operations-db`; xoá volume `smartpark-schema_facility-schema-data` + `smartpark-schema_operations-schema-data`; `up -d facility-db operations-db redis` (giữ nguyên user/payment/notification/iot/rabbitmq). 4 bảng mới + index được tạo tự động từ overlay.
- `seed-demo-site.ps1` OK (DEMO-HQ b0000000-…01, 48 slots). Build + `up -d` 5 image từ worktree; health 5035/5045/5055/5173 = 200; 4 login demo OK.
- Verify overlay wiring: `apply-workflow-migrations.ps1 -Service Parking -SchemaRoot .` và `-Service Operation -SchemaRoot .` → "schema already current; applied incremental features and verified required relations" (idempotent).
- **Newman 6.2.3: 33/33 requests, 33/33 test-scripts, 101/101 assertions, 0 failed, exit 0** (4.8s, avg 58ms). Secrets truyền qua `--env-var` từ `.env`, không commit.
- Bằng chứng DB sau run: `outbox_events` 16 dòng (OccupancyChanged=8, OccupancyReleased=8), published 16/16; `processed_events` = 16 → consumer đã tiêu thụ đủ, đúng 1 lần mỗi event. `occupancy_release_ledger` = 0 và `slot_sequence_watermarks` = 0 là ĐÚNG thiết kế (walk-in event không có slotId).
- **Lỗi authoring phát hiện khi chạy thật:** 8 test-script khai báo `const data = …`, nhưng `data` là global của Postman/Newman sandbox → `SyntaxError: Identifier 'data' has already been declared`. Đã rename `data` → `payload` (rename theo token, bỏ qua string/comment, không đụng `body.data`); suite xanh sau đó.
- **Phát hiện hub:** `POST /ws/parking-lots/{siteId}/layout/negotiate` trả 200 cho cả 3 trường hợp (không token / operator đúng site / operator site lạ) vì hub không có `[Authorize]` và service không đăng ký JWT auth; check scope chỉ chạy trong `OnConnectedAsync` của WebSocket thật → negotiate KHÔNG dùng để test 401/403.
- Dọn dữ liệu test: đóng 3 session ACTIVE còn sót của các run lỗi (endpoint check-out yêu cầu body `{"plateNumber": "..."}`; POST rỗng trả 415/400). Còn 0 ACTIVE.
- Đã cập nhật cả 2 guide theo kết quả live (status VERIFIED (live)/PARTIAL/BLOCKED + mục 4.1 bằng chứng, mục 9 viết lại).

### FE — nối hub realtime (ngoài WBS gốc) — DONE
- `FE/src/lib/operatorLayoutLive.ts` (87 dòng): `watchOperatorLayout(siteId, onLayoutChanged)`, URL `/ws/parking-lots/{siteId}/layout` trên `VITE_RESERVATION_API_BASE_URL` (5055); lắng nghe `SlotStateChanged`/`OccupancyChanged`/`OccupancyReleased` + `SnapshotRequired`; dùng `operatorRealtimeGuard.decide/commit` để chống trùng/stale; KHÔNG patch state cục bộ, chỉ gọi callback.
- `OperatorDashboard.tsx` (+6 dòng): thêm `useEffect` theo `selectedLot?.id` gọi `watchOperatorLayout` → `setRefreshKey(v => v+1)` để refetch snapshot REST (nguồn sự thật).
- Verify: `npx tsc --noEmit` 4 lỗi pre-existing (không tăng); `npx vite build` exit 0.

## 3. Chưa làm
- (không còn hạng mục bắt buộc). P4 đã chạy live và xanh; FE đã nối hub. Chỉ còn các case cần WS client/broker harness (liệt kê ở §7).

## 4. Quyết định đã chốt
- Q1: Redis backplane cho SignalR (ACCEPTED)
- Q2: Operations là nguồn sự thật occupancy (ACCEPTED)
- Q3: event_id riêng trong outbox, unique index (ACCEPTED)
- Q4: p95 ≤ 2s chỉ mục tiêu dev, không SLA (ACCEPTED)

## 5. Blockers còn mở
- (đã xong) apply-workflow-migrations.ps1: 2 overlay realtime đã wire (Parking → realtime-projection.sql, Reservation/Operation → realtime-outbox.sql, chạy ở nhánh `$features` nên áp được cả DB đã current) + 4 bảng mới đã thêm vào danh sách `$required` để verify.
- (đã xong) compose.app.yml: parking-service + reservation-service có `RabbitMq__ConnectionString` (amqp://…@rabbitmq:5672/${VHOST}); reservation-service có `Redis__ConnectionString` (opt-in qua `SMARTPARK_REDIS_CONNECTION`, rỗng = hub in-memory). Network `smartpark` = external `smartpark-schema_default` nên rabbitmq/redis dùng chung network, không cần thêm service.
- (đã xong) stack app giờ chạy image build từ worktree (5 service), DB facility/operations đã dựng lại từ overlay, redis + rabbitmq up; consumer chạy thật.
- 4 pre-existing FE TS errors (GroupedNumberInput, OperatorDashboard `SlotStatusFilter`, SiteManagement `setMessage`, useSiteManagement `reassignSite`) — không fix; `vite build` vẫn xanh.

## 6. Trạng thái hiện tại
P0–P1–P2–P3–P5 done. P2.1 đã rà soát & chỉnh code theo coding rule/skill. P4 đã viết đủ artifact **và chạy live xanh** (33/33 requests, 101/101 assertions, 0 failed) trên stack build từ chính branch này; outbox 16 event → consumer xử lý đủ 16. FE đã nối hub `/ws/parking-lots/{siteId}/layout`. Còn BLOCKED: hub auth/reconnect/delta (cần WS client), replay trùng event, ledger cho event có slotId, zone-only thật, p95 thật, Redis 2 instance.

## 7. Next step
- (đã xong) Chốt lệch doc↔code: đã gỡ eventId/sequenceNumber/siteId khỏi SlotVisualStateDto trong OpenAPI và sửa lỗi thụt lề block; `RealtimeEventEnvelope` vẫn giữ 3 field.
- (đã xong) P4 live: dựng lại facility/operations DB từ overlay + redis, seed demo site, build/up 5 image từ worktree, chạy `apply-workflow-migrations.ps1 -Service Parking|Operation` (idempotent OK), chạy Newman → xanh 100%.
- (đã xong) Merge checklist: 2 overlay realtime đã wire vào apply-workflow-migrations.ps1 (+ verify trong `$required`).
- (đã xong) FE nối hub `/ws/parking-lots/{siteId}/layout` qua `operatorLayoutLive.ts` + `RealtimeIdempotencyGuard`, gọi lại snapshot REST khi có event.
- (còn lại — cần harness) verify bằng SignalR client: `UNAUTHENTICATED` / `OPERATOR_LOT_UNASSIGNED` khi handshake (negotiate KHÔNG kiểm được), `SnapshotRequired` + reconnect, delta `SlotStateChanged` thật, p95 thật ≥20 mẫu.
- (còn lại — cần harness) replay/duplicate event (publish 2 lần cùng eventId), release-once ledger với event có `slotId` (cần seed reservation), zone-only thật (OPEN D0-5), Redis fan-out 2 instance.
- (rủi ro đã biết, defer) race cấp sequence producer: `Max(sequence_number)+1` theo site, index `(site_id, sequence_number)` không unique → check-in đồng thời cùng site có thể trùng số.
