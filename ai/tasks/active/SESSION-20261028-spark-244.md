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

### Merge SPARK-238 (30 commit mới) + dựng lại môi trường + chạy lại P4 — DONE (green)
- Fetch: `origin/feat/SPARK-238-facility-structure-port` tip `3b47c98b`, merge-base với nhánh 244 là `afc76df5` → **30 commit / 316 file** mới, trong đó `e14557fb "chore(merge): merge develop into the SPARK-238 branch"` kéo theo develop mới (PR #40 repo-cleanup, PR #41 fe-v2.3, SPARK-248 role consolidation).
- Pre-flight: `git merge-tree --write-tree` → merge sạch 0 conflict. Nhưng 4 file đang dirty trùng với 238 (`.env.example`, `FE/.../OperatorDashboard.tsx`, `compose.app.yml`, `scripts/apply-workflow-migrations.ps1`) → theo quyết định của user: commit checkpoint trước.
- **Commit checkpoint `70739521`** (28 path: toàn bộ P2 consumer + FE hub + 4 artifact test + overlay + compose + script) rồi **merge `2e94684e`** (`--no-ff`, exit 0, không conflict).
- Xác minh sau merge: 2 overlay vẫn wire (`apply-workflow-migrations.ps1` dòng 53/54 required + dòng 108/109 features, có cả `05.16-Parking-Owner-Onboarding.sql` + `overlay/parking-structure-maintenance.sql` của 238); `compose.app.yml` vẫn có `RabbitMq__ConnectionString` (parking + reservation) và `Redis__ConnectionString` (reservation); `compose.schema-integration.yml` vẫn mount 2 overlay realtime + service `redis`; `.env.example` vẫn có block Redis; `OperatorDashboard.tsx` vẫn import/gọi `watchOperatorLayout`. Tất cả file mới của SPARK-244 còn nguyên.
- Build sau merge: **UserService.API 0 warning/0 error**, **FacilityService.API 0/0**, **OperationService.API 0 error + 2 NU1902 MessagePack (pre-existing)**. `docker compose config --quiet` cả 2 file = exit 0; mọi bind-mount nguồn đều tồn tại.
- Dựng lại sạch: `compose.app.yml down` + `compose.schema-integration.yml down -v` (xoá toàn bộ volume 6 DB + rabbitmq + redis; giữ `workflow-keys`, `fe-node-modules`) → `up -d` → 8/8 container healthy; 4 bảng realtime (`outbox_events`, `processed_events`, `slot_sequence_watermarks`, `occupancy_release_ledger`) được tạo tự động từ init script của code đã merge; user-db có 21 bảng (schema SPARK-248 mới).
- **Vấn đề môi trường phát hiện:** `seed-demo-site.ps1` fail ở bước resolve account vì DB user mới tinh chưa có owner/operator; chạy `user-service` (DataSeeder) thì chỉ ra admin/owner/operator — **KHÔNG có `driver@gmail.com`**, vì `DataSeeder.SeedAsync` return ngay khi `Users.Any()` (DataSeeder.cs:26-29) và bootstrap role user chạy trước. Fix theo đúng script của repo: `scripts/repair-demo-identity.ps1 -Apply` → tạo driver + refresh hash 3 account demo; verify 4/4 login OK (role driver/owner/operator/admin).
- Build lại 5 image từ code đã merge + `up -d`; health 5035/5045/5055/5065/5173 = 200; env trong container xác nhận `RabbitMq__ConnectionString` (parking + reservation) và `Redis__ConnectionString=redis://redis:6379` (reservation).
- **Chạy Newman lần 1 (sau merge): 33/33 request nhưng 7/101 assertion fail — TẤT CẢ từ 1 nguyên nhân gốc `0.4 Login Driver` = 401 AUTH_FAILED** (5.4 và 5.6 fail dây chuyền do không có `driverToken`). Sau `repair-demo-identity.ps1 -Apply`: **33/33 requests, 33/33 test-scripts, 101/101 assertions, 0 failed, exit 0** (3.9s, avg 60ms).
- Bằng chứng DB sau run lại: `outbox_events` 8 dòng → 8 published, 0 pending; `processed_events` = 8 (đúng 1 lần/event); `occupancy_release_ledger` = 0, `slot_sequence_watermarks` = 0 (đúng thiết kế — walk-in không có slotId); 48/48 slot `AVAILABLE`.
- Kết luận: contract mà SPARK-244 phụ thuộc **không bị phá** bởi SPARK-248 — `GET /api/operators/site-scope` vẫn tồn tại (`OperatorsController.cs:39`, `[Authorize(Roles="operator")]`) và login vẫn trả role lowercase (`owner`/`operator`/`admin`/`driver`).
- **Bẫy môi trường #2 (phát hiện khi test FE localhost:5173):** FE báo *"Cannot reach the authentication server. Please try again."* dù `POST /api/auth/login` qua curl vẫn 200.
  - Nguyên nhân: develop mới (từ SPARK-238) đổi CORS UserService sang **config-driven** — `Program.cs:24-31` đọc `Cors:AllowedOrigins`, `compose.app.yml:54` map từ `${SMARTPARK_USER_CORS_ORIGINS:-}` (mặc định **rỗng**), nhưng `.env` của worktree (copy từ repo chính, tạo trước biến này) **không có** `SMARTPARK_USER_CORS_ORIGINS` (chỉ `.env.example:89` có).
  - Hệ quả: policy `Frontend` không có origin nào → preflight trả 204 **không có `Access-Control-Allow-Origin`** → browser chặn fetch → FE hiện lỗi "không tới được auth server".
  - Fix: thêm `SMARTPARK_USER_CORS_ORIGINS="http://localhost:5173;http://127.0.0.1:5173;http://localhost:8443;http://127.0.0.1:8443"` vào `.env` (file git-ignored) rồi `up -d --force-recreate user-service`.
  - Verify sau fix: preflight `:5035` → 204 + `ACAO=http://localhost:5173` + `ACAM=POST`; login thật kèm `Origin` → 200 + ACAO; preflight `:5045` và `:5055` cũng trả ACAO đúng (Facility/Operation vẫn hardcode/không đổi trong merge).

- **Bẫy môi trường #3 (phát hiện khi test C3 — container AI không start được):** `smartpark-ai-ai-rest-1` và `smartpark-ai-ai-live-1` đều `Exited (143)`, không start lại được.
  - Triệu chứng: `docker inspect` → `State.Error = failed to set up container networking: network e26aa3d311f491eb9... not found`.
  - **KHÔNG phải mất image, KHÔNG phải mất volume:** image `smartpark-ai:local` vẫn còn (build 2 tuần trước); `compose.ai.yml` không khai báo volume nào.
  - Nguyên nhân thật: 2 container được tạo từ trước, gắn vào `smartpark-schema_default` theo **network ID cũ** `e26aa3d3...`. Khi dựng lại sạch stack schema (`compose.schema-integration.yml down -v` rồi `up -d`), network bị xoá và tạo lại với **ID mới** `8cbee697...` → container giữ ID cũ nên Docker từ chối start. Đây là hệ quả của chính bước dựng lại môi trường ở trên, không phải lỗi SPARK-244.
  - Fix (chạy trong worktree 244): `docker compose --env-file .env -f compose.ai.yml up -d --force-recreate` — `name: smartpark-ai` trùng project cũ nên compose nhận lại 2 container; **không build lại image**, không đổi code.
  - Verify sau fix: cả 2 container `Up (healthy)`, `127.0.0.1:5001->5001` / `127.0.0.1:5002->5002`, network `smartpark-schema_default` (ID mới); probe 5001/5002 phản hồi HTTP (404 ở `/` là bình thường, healthcheck dùng socket). Quét toàn bộ `docker ps -a` → **0 container khác** còn `State.Error`.
  - Không ảnh hưởng C3: đường đi C3 chỉ cần `operations-db-1`, `facility-db-1`, `smartpark-rabbitmq`, user/parking/reservation service — tất cả vẫn healthy (5035/5045/5055 = 200).
  - Quy tắc phòng ngừa: sau **mỗi** lần `down -v` stack schema, mọi stack khai báo `external: true` với `smartpark-schema_default` (hiện có AI; sau này có thể thêm) phải `--force-recreate`. Kiểm tra nhanh:
    ```powershell
    docker ps -a --format "{{.Names}}" | ForEach-Object {
      $j = docker inspect $_ | ConvertFrom-Json
      if ($j.State.Error) { "$_ -> $($j.State.Error)" }
    }
    ```
  - Không liên quan: 2 container Sonar (`smartpark-sonar-sonarqube-1`, `smartpark-sonar-sonar-db-1`) vẫn `Exited (255)` từ 3 ngày trước — để nguyên.

- **Bẫy môi trường #4 (phát hiện khi test C3 — container user-service không khởi động được):** `smartpark-app-user-service-1` = `Exited (139)`, `health=unhealthy`, `State.Error` rỗng.
  - Triệu chứng trong log: `Npgsql.PostgresException 42P01: relation "account_roles" does not exist` ném từ `DataSeeder.SeedRoleUserAsync` ← `SeedAdminAsync` ← `UserHost.BuildAsync` (seeding lúc khởi động) → container chết ngay sau ~8s.
  - Nguyên nhân thật: 4 image backend đang chạy là **image CŨ (build 2026-10-29T17:07:3xZ, trước khi merge SPARK-248)** trong khi user-db đã theo schema **hậu SPARK-248** (bảng `account_roles` đã bị xoá — worktree test còn assert `to_regclass('public.account_roles') IS NULL`). Code cũ map `account_roles`, DB mới không có → 42P01. Bằng chứng độc lập: số dòng stack trace (`DataSeeder.cs:80`) không khớp source hiện tại (dòng 80 là `}`; query nằm ở 73–74), và source hiện tại dùng `Accounts` + `RoleCode`, không còn entity `AccountRole` (chỉ còn class `AccountRolePolicy`).
  - **KHÔNG phải lỗi SPARK-244**, cũng không phải lỗi schema/seed: chỉ là container chạy image cũ (app stack bị dựng lại từ nguồn/cache khác).
  - Fix: `docker compose --env-file .env -f compose.app.yml build user-service parking-service reservation-service payment-service` rồi `... up -d --force-recreate user-service parking-service reservation-service payment-service`. Sau build: user-service image `2026-11-02T14:09:52Z`, parking/reservation `2026-11-02T09:52:23Z` (cache-hit vì code không đổi sau merge), payment `2026-10-29T17:07:33Z`; frontend không cần đụng.
  - Verify sau fix: 4 container `Up (healthy)`; log user-service sạch; health 5035/5045/5055/5065/5173 = **200**; login 4/4 OK (owner/operator/admin/driver, role chữ thường); Newman P4 chạy lại **33/33 request, 101/101 assertion, 0 fail**; `outbox_events` 22/22 published (0 pending) và `processed_events` 22 = khớp outbox.
  - Quy tắc phòng ngừa: sau bất kỳ thao tác dựng lại stack, kiểm tra container ↔ image **thời điểm build** (`docker inspect <ct> --format '{{.Image}}'` rồi so `docker image inspect <id> --format '{{.Created}}'`) và đối chiếu image phải **mới hơn commit merge**. Triệu chứng kinh điển của image cũ: `42P01 relation "xxx" does not exist` khi seeding.

- **Bẫy khi test bằng PowerShell + curl.exe (quan trọng cho P4):** `curl.exe -d $body` với `$body` là chuỗi JSON do PowerShell tạo sẽ bị **PS 5.1 cắt dấu `\"` bên trong** → server nhận JSON hỏng → trả `400 VALIDATION_FAILED` với `errors[].field = "$"` / `"loginDto"`, **rất dễ bị hiểu nhầm là lỗi đăng nhập/sai mật khẩu**. Cách đúng: ghi body ra file rồi `--data-binary "@$file"`, hoặc dùng `Invoke-RestMethod -Body $json -ContentType 'application/json'`. (Newman/Postman không bị vấn đề này; các login trong collection vẫn 200.)

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

Cập nhật: nhánh đã **merge 30 commit mới của SPARK-238** (kèm develop mới + SPARK-248). Sau merge: 3 service build xanh, dựng lại sạch toàn bộ schema stack + seed + build lại 5 image, và **Newman vẫn xanh 33/33 requests – 101/101 assertions – 0 failed**; outbox 8/8 published, consumer 8/8. Trạng thái BLOCKED không đổi.

## 7. Next step
- (đã xong) Merge 30 commit mới của SPARK-238 vào nhánh 244: checkpoint `70739521` + merge `2e94684e`, 0 conflict; artifact SPARK-244 nguyên vẹn; 3 service build xanh.
- (đã xong) Dựng lại sạch môi trường từ code đã merge (`down -v` toàn bộ schema volume → `up`, 8/8 healthy, 4 bảng realtime tự tạo) + `seed-demo-site.ps1` + `repair-demo-identity.ps1 -Apply` (fix thiếu driver) + build/up 5 image; Newman chạy lại xanh 101/101.
- (đã xong) Chốt lệch doc↔code: đã gỡ eventId/sequenceNumber/siteId khỏi SlotVisualStateDto trong OpenAPI và sửa lỗi thụt lề block; `RealtimeEventEnvelope` vẫn giữ 3 field.
- (đã xong) P4 live: dựng lại facility/operations DB từ overlay + redis, seed demo site, build/up 5 image từ worktree, chạy `apply-workflow-migrations.ps1 -Service Parking|Operation` (idempotent OK), chạy Newman → xanh 100%.
- (đã xong) Merge checklist: 2 overlay realtime đã wire vào apply-workflow-migrations.ps1 (+ verify trong `$required`).
- (đã xong) FE nối hub `/ws/parking-lots/{siteId}/layout` qua `operatorLayoutLive.ts` + `RealtimeIdempotencyGuard`, gọi lại snapshot REST khi có event.
- (còn lại — cần harness) verify bằng SignalR client: `UNAUTHENTICATED` / `OPERATOR_LOT_UNASSIGNED` khi handshake (negotiate KHÔNG kiểm được), `SnapshotRequired` + reconnect, delta `SlotStateChanged` thật, p95 thật ≥20 mẫu.
- (đã xong — C4b) release-once **ledger** với event có `slotId`: phủ bằng integration test `tests/Services/SmartParking.FacilityService.Tests/OccupancyProjectionStoreTests.cs` (4/4 pass) — gọi thẳng `OccupancyProjectionStore.ApplyAsync` trên DB thật. Xem §8.
- (còn lại — cần harness) replay/duplicate event qua broker thật (publish 2 lần cùng eventId) — tầng `ApplyAsync` đã phủ ở C4b; zone-only thật (OPEN D0-5), Redis fan-out 2 instance.
- (rủi ro đã biết, defer) race cấp sequence producer: `Max(sequence_number)+1` theo site, index `(site_id, sequence_number)` không unique → check-in đồng thời cùng site có thể trùng số.

## 8. C4b — phủ nhánh release-once ledger bằng integration test (DONE)
Lý do: `occupancy_release_ledger` **chưa từng được ghi** trong mọi lần chạy (luôn 0 dòng) vì walk-in phát `OccupancyReleased { sessionId, slotId: null }` ⇒ consumer bỏ qua nhánh slot. Nhánh SQL đó là vùng mù, không thể chạm bằng Postman (Postman không publish RabbitMQ / không insert Postgres, và không có REST route tạo reservation).

Đã làm:
- Thêm `tests/Services/SmartParking.FacilityService.Tests/OccupancyProjectionStoreTests.cs` (4 test, `[StructurePostgresFact]`): (1) `DuplicateEventIdIsAppliedOnce`; (2) `SpentReleaseLedgerDoesNotFreeASlotReoccupiedByAnotherSession` — slot bị phiên B chiếm lại rồi replay release của phiên A, slot **phải vẫn** `OCCUPIED` và ledger của A vẫn 1 dòng (đây chính là bug ledger sinh ra để chặn); (3) `ReleaseWithoutSlotIsRecordedButNotProjected`; (4) `StaleSequenceIsIgnoredAfterTheWatermarkAdvances`.
- Mỗi test tự `CREATE DATABASE occupancy_projection_test_<guid>`, nạp DDL thật (`02-facility-service-db.sql` + `parking-tenants.sql` + `realtime-projection.sql`), seed tenant/site/zone/slot, rồi `DROP DATABASE ... WITH (FORCE)`.
- Lưu ý ngữ nghĩa: `ApplyAsync` trả `true` = "event có slotId và đã qua cửa kiểm tra", **không** phải "đã đổi dòng" ⇒ test assert trạng thái DB.
- Sửa bắt buộc để build được: `ReservationGateUseCaseTests.cs` +9 dòng thêm `AuthorizeSiteAsync` cho `FakeOperatorIdentity` (commit `70739521` đổi interface mà không cập nhật fake ⇒ **CS0535**, cả assembly test không build).

Kết quả chạy thật (PK từ `.env`, chỉ truyền qua biến môi trường, không in ra):
- `dotnet test ... --filter "FullyQualifiedName~OccupancyProjectionStore"` → **Passed: 4, Failed: 0, Skipped: 0** (16 s).
- `dotnet test ...` toàn project → **Passed: 173, Failed: 1, Skipped: 0**; test fail là `PostgresStructureWorkflowTests.GateConnectionsLeaveATwoConnectorQueryPoolAvailable` (timeout 5 s trong `NpgsqlConnection.OpenAsync`), **fail cả khi chạy một mình** ⇒ lỗi có sẵn của môi trường, không liên quan SPARK-244. `max_connections=100`, chỉ 7 connection đang dùng.
- Yêu cầu môi trường: `SMARTPARK_STRUCTURE_TEST_CONNECTION='Host=localhost;Port=5442;Database=smartpark_facility;Username=smartpark_facility;Password=<SMARTPARK_DEV_PASSWORD>'`; không set thì test **skip** chứ không fail.

Cập nhật tài liệu (đều chưa commit): branch guide thêm cột **Loại** vào §3 (Chức năng / Đường ống dữ liệu / Độ bền dữ liệu / Hợp đồng / Tài liệu), đổi tên C4 thành "Release-once ở **biên API**" kèm cảnh báo phạm vi, thêm mục **C4b** đầy đủ (bảng 3 lớp bảo vệ, lệnh, 4 kịch bản, PASS/FAIL, lưu ý 173/1 + CS0535); manual guide viết lại §8.3 (trỏ C4b) và cập nhật §10 hai dòng 03.7 / 02.6 (từ "BLOCKED" sang "Postman-only — đã phủ bởi C4b").

## 9. Report/backlog ngoài repo
- Tạo `D:\My file\FA26 - OJT FSoft\Report\future_improvement.md` — backlog sống ghi các điểm cải thiện (FI-01→FI-20) + 7 bẫy môi trường (EP-01→EP-07), mỗi mục có: bằng chứng (file/dòng, lệnh, kết quả thật), tác động, đề xuất, chi phí, quyết định — dùng làm bằng chứng khi PM hỏi lý do lựa chọn. Fi-20 đánh dấu RESOLVED. Kèm §4 "ưu tiên đề xuất" và §5 mẫu thêm mục mới, §6 nhật ký.
