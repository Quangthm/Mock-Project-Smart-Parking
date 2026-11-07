# SPARK-244 — Branch Verification & Manual Test Guide

Branch: `feat/SPARK-244-realtime-monitoring` @ `2e94684e` (đã merge 30 commit của SPARK-238 + develop/SPARK-248).
Scope: US-O03 / sprint-3 — Operator realtime monitoring: snapshot vận hành, outbox → RabbitMQ → consumer projection, hub realtime, assigned-lot scope, release-once, zone-only reserved count, chống replay, mục tiêu latency dev.
Contract: 3D Digital Twin `v1.2.0` + `docs/03-design/api/3d-digital-twin-spark-244-delta-design.md` (G1–G6).

> **Status legend**
> **VERIFIED** = đã chạy thật và có kết quả ghi lại bên dưới.
> **TO RUN** = cần bạn chạy trên stack đang bật; chưa có kết quả.
> **BLOCKED** = không thể pass bằng Postman/HTTP (cần SignalR client, broker harness, hoặc tính năng chưa có).

> **Tình trạng hiện tại.** Stack đang chạy **chính code của branch này** (image build từ worktree, FE bind-mount worktree). Collection đã được chạy thật 2 lần (trước và sau khi merge 238) và **xanh**: 33/33 request, 101/101 assertion, 0 fail. Các mục còn **BLOCKED** là những mục cần WebSocket client hoặc bơm message trực tiếp vào RabbitMQ — không phải lỗi của SPARK-244.

> **Ba bẫy môi trường phải xử lý trước khi test** (nếu không sẽ tưởng là bug):
> 1. `.env` phải có `SMARTPARK_USER_CORS_ORIGINS="http://localhost:5173;http://127.0.0.1:5173;http://localhost:8443;http://127.0.0.1:8443"`. Thiếu ⇒ preflight không có `Access-Control-Allow-Origin` ⇒ FE báo *"Cannot reach the authentication server"* dù Postman/curl vẫn 200.
> 2. Trên DB user mới tinh, `DataSeeder` tạo admin/owner/operator nhưng **không** tạo `driver@gmail.com` (nó return ngay khi đã có user). Chạy `./scripts/repair-demo-identity.ps1 -Apply` trước khi chạy collection.
> 3. `seed-demo-site.ps1` chỉ chạy **sau** khi UserService đã tạo xong account demo.

---

## 0. Điều gì đã thay đổi (để biết cần test gì)

| # | Hạng mục | Nội dung | Bằng chứng trong worktree |
|---|---|---|---|
| a | P0 design | Delta design đóng G1–G6 (envelope `eventId`+`sequenceNumber`, release-once, zone-only, 403 scope) | `docs/03-design/api/3d-digital-twin-spark-244-delta-design.md` |
| b | P1 infra | `redis:7-alpine` + healthcheck; `SMARTPARK_REDIS_*` | `compose.schema-integration.yml`, `.env.example` |
| c | P2 outbox | Bảng `outbox_events` (operations-db), ghi trong cùng transaction nghiệp vụ | `scripts/database/overlay/realtime-outbox.sql`, `Persistence/ReservationGateStore.cs` |
| d | P2 publisher | Worker poll `published_at IS NULL` + `FOR UPDATE SKIP LOCKED`, publish sang topic exchange `smartpark.occupancy` | `Infrastructure/Outbox/OccupancyOutboxWorker.cs`, `RabbitMqOutboxPublisher.cs` |
| e | P2 hub | `/ws/parking-lots/{siteId}/layout`, group `site:{siteId}:operators`, gửi `SnapshotRequired` | `API/Hubs/ParkingLotLayoutHub.cs` |
| f | P2 broadcaster | Sau khi publish thì đẩy delta xuống hub | `API/Realtime/SignalRLayoutRealtimeBroadcaster.cs` |
| g | P2 consumer | `processed_events` + `slot_sequence_watermarks` + `occupancy_release_ledger`, cập nhật `parking_slots` | `FacilityService/Infrastructure/OccupancyProjectionWorker.cs`, `Persistence/OccupancyProjectionStore.cs` |
| h | P2.1 compliance | Tách DB access của consumer ra khỏi Infrastructure về Persistence; bỏ config chết | — |
| i | P3 FE | Guard chống trùng event + nối hub vào dashboard | `FE/src/lib/realtimeIdempotency.ts`, `FE/src/lib/operatorLayoutLive.ts`, `OperatorDashboard.tsx` |
| j | P4 | Postman collection + environment + 2 guide | `docs/04-testing/**` |
| k | Merge | 30 commit SPARK-238 + develop/SPARK-248 | commit `2e94684e` |

---

## 1. Chuẩn bị môi trường

### 1.1. Stack đang chạy

| Thành phần | URL / container | Ghi chú |
|---|---|---|
| Frontend (Vite dev) | `http://localhost:5173` | Container bind-mount **chính worktree này** (`SmartParking-244\FE`). Sửa FE xong chỉ cần **F5**; Vite hot-reload. |
| UserService | `http://localhost:5035` | Login + `api/operators/site-scope`. |
| FacilityService | `http://localhost:5045` | `operational-layout`, `operator-layout`, route nội bộ `/internal/**`. |
| OperationService | `http://localhost:5055` | `parking-sessions`, outbox, hub `/ws/...`. |
| PaymentService | `http://localhost:5065` | Không dùng trong test này. |
| Facility DB | `facility-db-1` → `127.0.0.1:5442` | DB `smartpark_facility`. Chứa 3 bảng projection. |
| Operations DB | `operations-db-1` → `127.0.0.1:5443` | DB `smartpark_operations`. Chứa `outbox_events`. |
| RabbitMQ | `smartpark-rabbitmq` (`5672`, UI `15672`) | Exchange `smartpark.occupancy`. |
| Redis | `smartpark-redis` (`6379`) | Backplane SignalR, `Redis__ConnectionString=redis://redis:6379`. |

App stack chạy image `smartpark/<service>:local` **build từ worktree này**, không phải repo `develop`. Không cần build lại khi chỉ chạy test.

### 1.2. Tài khoản và dữ liệu seed

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Owner | `owner@smartpark.local` | `.env` → `SMARTPARK_OWNER_PASSWORD` |
| Operator (đã gán DEMO-HQ) | `operator@smartpark.local` | `.env` → `SMARTPARK_OPERATOR_PASSWORD` |
| Admin | `admin@smartpark.local` | `.env` → `SMARTPARK_ADMIN_PASSWORD` |
| Driver | `driver@gmail.com` | `Password@123` (seed công khai, không phải secret) |

ID seed cố định:

- Tenant: `a0000000-0000-4000-8000-000000000001`
- Site: `b0000000-0000-4000-8000-000000000001` (DEMO-HQ)
- Floor: `c0000000-0000-4000-8000-000000000001` · Zone: `c0000000-0000-4000-8000-000000000002`
- Site "lạ" để test 403: `00000000-0000-4000-8000-00000000dead` (`foreignSiteId` trong environment)

Service key cho route `/internal/**`: `.env` → `SMARTPARK_SERVICE_KEY` (gửi qua header `X-Service-Key`).

Không dán mật khẩu/token vào tài liệu hay commit. Environment của Postman để trống 3 biến `ownerPassword` / `operatorPassword` / `adminPassword` / `serviceKey` và bạn điền lúc chạy.

### 1.3. Kiểm tra nhanh trước khi test

```powershell
docker ps --filter name=smartpark-app- --format "{{.Names}}`t{{.Status}}"
docker ps --filter name=facility-db-1 --format "{{.Names}}`t{{.Status}}"
docker ps --filter name=operations-db-1 --format "{{.Names}}`t{{.Status}}"
docker ps --filter name=smartpark-rabbitmq --format "{{.Names}}`t{{.Status}}"
docker ps --filter name=smartpark-redis --format "{{.Names}}`t{{.Status}}"

foreach ($p in 5035,5045,5055,5065) { curl.exe -s -o NUL -w "$p %{http_code}`n" "http://localhost:$p/health" }
curl.exe -s -o NUL -w "5173 %{http_code}`n" http://localhost:5173/
```

Tất cả phải **200** (container phải `Up`/`healthy`).

Xác nhận FE đang phục vụ code của worktree này (phải in `containsMarker=YES`):

```powershell
$m = curl.exe -s "http://localhost:5173/src/lib/operatorLayoutLive.ts"
if ($m -match 'watchOperatorLayout') { 'containsMarker=YES' } else { 'containsMarker=NO' }
```

Nếu `NO` ⇒ container đang mount repo khác:

```powershell
docker compose --env-file .env -f compose.app.yml up -d --force-recreate frontend
```

Kiểm tra CORS (nguyên nhân số 1 của lỗi login FE):

```powershell
curl.exe -s -i -X OPTIONS "http://localhost:5035/api/auth/login" `
  -H "Origin: http://localhost:5173" `
  -H "Access-Control-Request-Method: POST" | Select-String "HTTP/|Access-Control-Allow-Origin"
```

**PASS:** `204` (hoặc `200`) và có dòng `Access-Control-Allow-Origin: http://localhost:5173`.
**FAIL:** không có header đó ⇒ xem bẫy môi trường #1 ở đầu tài liệu, thêm biến vào `.env` rồi `up -d --force-recreate user-service`.

### 1.4. Đăng nhập (lấy bearer token)

```powershell
$body  = @{ email = 'operator@smartpark.local'; password = '<REAL_OPERATOR_PASSWORD>' } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri 'http://localhost:5035/api/auth/login' `
  -ContentType 'application/json' -Body $body
$token = $login.data.accessToken
$headers = @{ Authorization = "Bearer $token" }
"role=$($login.data.user.role) tokenLen=$($token.Length)"
```

**PASS:** HTTP `200`, `success = true`, `data.user.role` ∈ `owner|operator|admin|driver` (chữ thường), token khác rỗng.
**FAIL:** `AUTH_FAILED` = sai mật khẩu (lấy đúng từ `.env`); `Connection refused` = UserService chưa lên; `0.4 Login Driver` 401 = thiếu account driver ⇒ chạy `./scripts/repair-demo-identity.ps1 -Apply`.

### 1.5. Ghi baseline 4 bảng realtime (trước khi chạy)

```powershell
# operations-db
docker exec operations-db-1 psql -w -U smartpark_operations -d smartpark_operations -c `
"select event_type, count(*) total, count(published_at) published from outbox_events group by 1 order by 1;"

# facility-db
docker exec facility-db-1 psql -w -U smartpark_facility -d smartpark_facility -c `
"select (select count(*) from processed_events) processed,
        (select count(*) from slot_sequence_watermarks) watermarks,
        (select count(*) from occupancy_release_ledger) ledger,
        (select count(*) from parking_slots where physical_state='OCCUPIED') occupied;"
```

Baseline khi viết tài liệu: `outbox_events` 8 dòng (8 published, 0 pending), `processed_events` 8, `watermarks` 0, `ledger` 0, `occupied` 0, `parking_slots` 71.

> **Đọc số cho đúng:** mỗi vòng walk-in sinh **2** event (`OccupancyChanged` lúc check-in, `OccupancyReleased` lúc check-out). Chạy trọn collection (2.1/2.4 và 6.2/6.4) ⇒ outbox và processed tăng **+4**. `watermarks` và `ledger` **vẫn 0** vì walk-in không có `slotId` — đây là đúng thiết kế, không phải consumer hỏng.

---

## 2. Bản đồ điều hướng UI (Operator)

```
http://localhost:5173
  └─ Đăng nhập operator@smartpark.local
      └─ Operator dashboard
          ├─ Dropdown chọn lot        <-- chọn DEMO-HQ; đổi lot = đổi site của hub
          ├─ Danh sách slot / trạng thái
          └─ (ẩn) hub client: ws://localhost:5055/ws/parking-lots/<siteId>/layout
```

Hub chỉ kết nối cho **lot đang được chọn**. Đổi lot ⇒ unsubscribe lot cũ, connect lot mới, guard idempotency được `reset()`.

Cần mở **DevTools → Network → WS** để xem: URL hub, status `101`, và các frame (`SnapshotRequired`, `SlotStateChanged`, `OccupancyChanged`, `OccupancyReleased`).

---

## 3. Checklist tổng

| # | Loại | Nhóm | Mục cần kiểm | Kết quả mong đợi | Trạng thái |
|---|---|---|---|---|---|
| C1 | Chức năng | Postman | Chạy toàn bộ collection (33 request) | 0 fail, 101 assertion xanh | **VERIFIED** (đã chạy 2 lần) |
| C2 | Chức năng | Postman | Snapshot vận hành đúng contract (folder 01) | 200 + đủ field; 400/404 đúng mã | **VERIFIED** |
| C3 | Đường ống dữ liệu | Postman + DB | Walk-in → outbox → consumer (folder 02) | 4 event mới, tất cả published, processed == outbox | **VERIFIED** |
| C4 | Độ bền dữ liệu (biên API) | Postman + DB | Release-once ở tầng API (folder 03) | claim 200 → release 204 → release 204 (no-op) → slot AVAILABLE | **VERIFIED** |
| C4b | Độ bền dữ liệu (ledger) | xUnit + Postgres | Release-once qua `OccupancyProjectionStore` (G3 thật) | 4/4 test xanh: dedup eventId, ledger chặn giải phóng lần 2, walk-in không ghi ledger, sequence cũ bị bỏ | **VERIFIED** (4/4 pass) |
| C5 | Hợp đồng (chức năng chưa có) | Postman | Zone-only reserved count (folder 04) | `zoneOnlyReservedCount` số, khớp số slot RESERVED của zone | **VERIFIED** |
| C6 | Chức năng (phân quyền) | Postman | Operator scope (folder 05) | 403 `OPERATOR_LOT_UNASSIGNED`; driver 403 | **VERIFIED** |
| C7 | Chức năng (hiệu năng) | Postman | Latency proxy (folder 06) | read ≤ 2000 ms; write→read ≤ 2000 ms | **VERIFIED** |
| C8 | Chức năng chính (realtime) | FE (browser) | Realtime end-to-end: connect → `SnapshotRequired` → delta → refetch | WS `101`, frame đúng, snapshot refresh ≤ 2 s, không nhấp nháy | **TO RUN** |
| C9 | Chức năng chính (realtime) | FE (browser) | Reconnect + token + negative scope | restart service ⇒ tự nối lại + 1 snapshot; sai scope ⇒ không nhận event | **TO RUN** |
| C10 | Tài liệu | Docs | Xác nhận các mục BLOCKED (không phải bug) | Đúng như mô tả ở §5 | **TO RUN** |

**Cột "Loại"** cho biết case đang chứng minh cái gì, để không đọc nhầm mức độ bao phủ:

- **Chức năng** — người dùng cuối nhìn thấy / dùng được.
- **Đường ống dữ liệu** — không có màn hình, chỉ chứng minh dữ liệu đi đúng đường.
- **Độ bền dữ liệu** — chứng minh hệ thống chịu được retry/trùng lặp; **không** phải một màn hình chức năng.
- **Hợp đồng** — kiểm chứng shape API của một tính năng **chưa được xây** (xem C5).
- **Tài liệu** — xác nhận các mục BLOCKED là đúng thiết kế, không phải regression.

---

## 4. Chi tiết từng case

### C1 — Chạy toàn bộ Postman collection

**Kiểm gì:** toàn bộ mặt HTTP của SPARK-244 (snapshot, walk-in session, outbox, claim/release nội bộ, scope, latency proxy) chạy đúng trên stack thật.

**Điều kiện trước:**
- Mục 1.3 xanh (5 port 200, container up).
- Đã chạy `./scripts/repair-demo-identity.ps1 -Apply` (đủ 4 account).
- Import 2 file:
  - `docs/04-testing/postman/SmartParking-Realtime-Layout-SPARK-244.postman_collection.json`
  - `docs/04-testing/postman/SmartParking-Realtime-Layout-SPARK-244.postman_environment.json`

**Các bước:**
1. Chọn environment `SmartParking - SPARK-244 Realtime Layout (local)`.
2. Điền biến secret (để trống trong repo):
   - `ownerPassword` ← `SMARTPARK_OWNER_PASSWORD`
   - `operatorPassword` ← `SMARTPARK_OPERATOR_PASSWORD`
   - `adminPassword` ← `SMARTPARK_ADMIN_PASSWORD`
   - `serviceKey` ← `SMARTPARK_SERVICE_KEY`
   - `driverPassword` đã có sẵn `Password@123`.
3. Kiểm tra `baseUrl=http://localhost:5045`, `userServiceUrl=http://localhost:5035`, `reservationServiceUrl=http://localhost:5055`.
4. Chạy folder **`00 - Setup (run first)`** trước. Phải xanh cả 4 request (owner/operator/admin/driver) trước khi chạy tiếp.
5. Chạy lần lượt **01 → 06**. Lần đầu **tắt** "Stop run on error" để thấy hết lỗi dây chuyền.

**PASS:**
- **33/33 request**, **33/33 test-script**, **101/101 assertion**, 0 fail (thời gian thực tế ~4 s).
- Các mã đặc biệt phải đúng:
  - `1.3` → **400** `INVALID_VEHICLE_TYPE`; `1.4` → **400** `INVALID_FLOOR`; `1.6` → **404** `SITE_NOT_FOUND`
  - `2.3` → **409** `VEHICLE_ALREADY_INSIDE`
  - `3.1` → **401** (thiếu `X-Service-Key`); `3.4`/`3.5` → **204**
  - `5.2` → **403** `OPERATOR_LOT_UNASSIGNED`; `5.3`/`5.4` → **403**; `5.6` → **403** `FORBIDDEN`

**FAIL:**
- `0.1–0.4` trả 401 ⇒ sai mật khẩu hoặc thiếu account (bẫy #2).
- Bất kỳ `500`, `503` ⇒ xem log:
  ```powershell
  docker logs --since 10m smartpark-app-reservation-service-1 2>&1 | Select-String "fail:|Exception" | Select-Object -Last 20
  docker logs --since 10m smartpark-app-parking-service-1     2>&1 | Select-String "fail:|Exception" | Select-Object -Last 20
  ```
- `5.6` trả `UNAUTHENTICATED` thay vì `FORBIDDEN` ⇒ `driverToken` rỗng ⇒ 0.4 fail.

**BLOCKED:** không.

---

### C2 — Snapshot vận hành đúng contract (folder 01)

**Kiểm gì:** `GET /api/parking-lots/{siteId}/operational-layout` trả đúng contract v1.2.0, đúng lọc, đúng lỗi; và snapshot **không** chứa `eventId`/`sequenceNumber`/`siteId` ở từng slot (ba field đó thuộc envelope realtime — G5).

**Điều kiện trước:** C1 folder 00 xanh; site DEMO-HQ đã seed.

**Các bước:**
1. `1.1` Owner snapshot → đọc `data`.
2. `1.2` thêm `?vehicleType=CAR`.
3. `1.3` `?vehicleType=BOGUS` → 400.
4. `1.4` `?floor=__NOPE__` → 400.
5. `1.5` gọi không token → 200 (bản sanitized).
6. `1.6` gọi `foreignSiteId` → 404.

**PASS:**
- `1.1` 200, có: `siteId`, `siteCode`, `siteName`, `contractVersion=1.2.0`, `generatedAt`, `isFresh`, `stalenessThresholdSeconds=30`, `metrics{totalPhysicalCapacity, occupiedCount, reservedCount, availableCarSlots, motorcycleCapacity, zoneCapacities}`, `floors[]`, `slots[]`.
- Mỗi `slots[]`: `slotId, slotCode, floorId, floorCode, supportedVehicleType, slotType, physicalState, reservationState, isEvCharging, isBookable, coordinates3d, features, updatedAt` (khóa ĐÚNG là `coordinates3d`).
- **Không** có `slots[].eventId` / `slots[].sequenceNumber` / `slots[].siteId`.
- `1.2` mọi slot có `supportedVehicleType = CAR`.
- `1.5` 200 và vẫn có `data.slots`, `data.metrics` (không 401).

**FAIL:** thiếu field contract; `coordinates3D` thay vì `coordinates3d`; snapshot lại chứa 3 field idempotency (nghĩa là DTO bị sửa sai); `1.5` trả 401.

**`1.5` trả 401 là FAIL thật** — route này cố ý cho phép anonymous nhưng sanitized.

**BLOCKED:** không.

---

### C3 — Outbox → RabbitMQ → consumer (folder 02)

**Kiểm gì:** mỗi thao tác check-in/check-out ghi **một** event vào outbox trong cùng transaction, worker publish hết, consumer áp dụng **đúng một lần** vào projection.

**Điều kiện trước:** C1 xanh; đã ghi baseline ở 1.5.

**Các bước:**
1. `2.1` walk-in check-in (`siteId=seededSiteId`). Kỳ vọng **200**, `data.id`, `data.licensePlate`, `data.vehicleType=CAR`, `data.slotId=null`, `data.slotNumber=null`, `data.exitTime=null`.
2. `2.2` `GET /api/parking-sessions?siteId=...` → thấy session vừa tạo, `status=ACTIVE`.
3. `2.3` check-in lại cùng biển → **409** `VEHICLE_ALREADY_INSIDE`.
4. `2.4` walk-in check-out → **200**, `data.exitTime` khác null.
5. Đợi ~2–3 s (worker poll 2 s) rồi chạy SQL ở 1.5.

**PASS:**
- `outbox_events`: số dòng tăng **+2** so với baseline; `published` = `total`; `published_at IS NULL` = **0**; type gồm `OccupancyChanged` và `OccupancyReleased`.
- `processed_events` tăng **+2** và bằng tổng `outbox_events`.
- `watermarks` = `ledger` = **0** (walk-in không có `slotId`).

**FAIL:**
- `published_at IS NULL` > 0 sau >10 s ⇒ publisher không chạy (kiểm tra `RabbitMq__ConnectionString` của `reservation-service`).
- `processed_events` < `outbox_events` ⇒ consumer không chạy (kiểm tra `parking-service`).
- Có dòng `processed_events` nhiều hơn số event ⇒ dedup hỏng.

**Kiểm tra message trên broker (tùy chọn):** mở `http://localhost:15672` (user/pass trong `.env` `SMARTPARK_RABBITMQ_*`) → exchange `smartpark.occupancy` → routing key `occupancy.changed` / `occupancy.released` xuất hiện.

> **Bẫy:** `2.4` phải gửi `Content-Type: application/json` **và** body `{"plateNumber":"<biển số>"}`. Thiếu content-type ⇒ **415**; body `{}` ⇒ **400**. Collection đã đóng gói đúng header+body này.

**BLOCKED:** replay/duplicate thật (bơm cùng một `eventId` 2 lần) — xem C10.

---

### C4 — Release-once ở **biên API** (folder 03)

> **Phạm vi (đọc trước khi kết luận):** C4 chỉ chứng minh idempotency ở **endpoint internal** `POST /internal/gate-sites/{siteId}/slots/{slotId}/release` (conditional `WHERE physical_state='OCCUPIED'`). Nó **không** đi qua outbox → RabbitMQ → consumer, nên **không** chứng minh cơ chế release-once của G3 (`occupancy_release_ledger`). Cơ chế đó nằm ở **C4b** ngay bên dưới.

**Kiểm gì:** claim đặt slot `OCCUPIED`; release lần 1 trả slot về `AVAILABLE`; release lần 2 **không đổi gì** (conditional `WHERE physical_state='OCCUPIED'`) nhưng vẫn 204 (idempotent ở tầng API).

**Điều kiện trước:** C1 xanh; site còn slot trống; `.env` có `SMARTPARK_SERVICE_KEY`.

**Chuẩn bị — dán 1 lần, dùng lại cho cả C4 → C7.** Mở PowerShell tại root worktree:

```powershell
$USER='http://localhost:5035'; $BASE='http://localhost:5045'; $OPS='http://localhost:5055'
$SITE='b0000000-0000-4000-8000-000000000001'      # DEMO-HQ
$TENANT='a0000000-0000-4000-8000-000000000001'
$FOREIGN='00000000-0000-4000-8000-00000000dead'   # site lạ (operator KHÔNG được gán)
function GetEnvVar([string]$name){ ((Get-Content .\.env | Where-Object { $_ -match "^$name=" }) -replace "^$name=",'').Trim().Trim('"').Trim("'") }
function Token([string]$email,[string]$pw){ (Invoke-RestMethod -Method Post -Uri "$USER/api/auth/login" -ContentType 'application/json' -Body (@{email=$email;password=$pw}|ConvertTo-Json -Compress)).data.accessToken }
$KEY        = GetEnvVar 'SMARTPARK_SERVICE_KEY'
$svcH       = @{ 'X-Service-Key' = $KEY }
$ownerToken = Token 'owner@smartpark.local'    (GetEnvVar 'SMARTPARK_OWNER_PASSWORD')
$opToken    = Token 'operator@smartpark.local' (GetEnvVar 'SMARTPARK_OPERATOR_PASSWORD')
$drvToken   = Token 'driver@gmail.com' 'Password@123'
$ownerH = @{ Authorization = "Bearer $ownerToken" }
$opH    = @{ Authorization = "Bearer $opToken" }
$drvH   = @{ Authorization = "Bearer $drvToken" }
```

> ⚠️ **Đừng dùng `curl.exe -d $body` trong PowerShell 5.1.** PS 5.1 cắt hết dấu `"` bên trong biến khi truyền sang file `.exe` (JSON 59 ký tự → server chỉ nhận 51 byte) nên API trả `400 VALIDATION_FAILED` — rất dễ hiểu nhầm thành "sai mật khẩu/hỏng API". Với POST hãy dùng `Invoke-RestMethod` / `Invoke-WebRequest` như dưới đây (hoặc `curl.exe --data-binary "@body.json"`). Với **GET** thì `curl.exe` an toàn vì không có body.

**Các bước**

**Cách A — Postman/Newman (nhanh nhất):** mở folder `03 - Release-once`, bấm **Send** lần lượt `3.1 → 3.6` **đúng thứ tự** (vì `3.2` lưu `claimedSlotId` cho `3.4`–`3.6`).
Hoặc bằng dòng lệnh: `npx --yes newman run <collection> -e <environment> --folder "03 - Release-once" --env-var serviceKey=...` (phải chạy folder `00 - Setup` trước — xem §6.2).

**Cách B — tự thao tác bằng PowerShell:**

1. `3.1` claim **thiếu** `X-Service-Key` → phải `401`:
   ```powershell
   try { (Invoke-WebRequest -Method Post -Uri "$BASE/internal/gate-sites/$SITE/slots/claim" -ContentType 'application/json' -Body (@{tenantId=$TENANT;vehicleType='CAR'}|ConvertTo-Json -Compress) -UseBasicParsing).StatusCode }
   catch { [int]$_.Exception.Response.StatusCode }      # mong đợi: 401
   ```
2. `3.2` claim **có** key → `200`, ghi lại `id` của slot vừa chiếm:
   ```powershell
   $claim = Invoke-RestMethod -Method Post -Uri "$BASE/internal/gate-sites/$SITE/slots/claim" -ContentType 'application/json' -Headers $svcH `
     -Body (@{tenantId=$TENANT;vehicleType='CAR';preferredSlotId=$null;targetUnitId=$null;allowReservedSlot=$false}|ConvertTo-Json -Compress)
   $claim.id; $claim.code; $claim.vehicleType          # ví dụ: <guid> / CarParkingSpace_001 / CAR
   ```
3. `3.3` đọc snapshot → đúng slot đó phải `OCCUPIED`:
   ```powershell
   $snap = (Invoke-RestMethod -Method Get -Uri "$BASE/api/parking-lots/$SITE/operational-layout" -Headers $ownerH).data
   ($snap.slots | Where-Object { $_.slotId -eq $claim.id }).physicalState     # mong đợi: OCCUPIED
   ```
4. `3.4` release lần 1 → `204`, body rỗng:
   ```powershell
   try { (Invoke-WebRequest -Method Post -Uri "$BASE/internal/gate-sites/$SITE/slots/$($claim.id)/release" -ContentType 'application/json' -Headers $svcH -Body (@{tenantId=$TENANT}|ConvertTo-Json -Compress) -UseBasicParsing).StatusCode }
   catch { [int]$_.Exception.Response.StatusCode }      # mong đợi: 204
   ```
5. `3.5` release lần 2 — **chạy lại y hệt lệnh bước 4** → vẫn `204`, không 409/500.
6. `3.6` đọc snapshot lại → slot trở về `AVAILABLE` / `UNRESERVED` / `isBookable=true`:
   ```powershell
   $s = ((Invoke-RestMethod -Method Get -Uri "$BASE/api/parking-lots/$SITE/operational-layout" -Headers $ownerH).data.slots | Where-Object { $_.slotId -eq $claim.id })
   "$($s.physicalState) / $($s.reservationState) / $($s.isBookable)"         # mong đợi: AVAILABLE / UNRESERVED / True
   ```

**Kiểm thêm bằng DB (không bắt buộc — chứng minh endpoint internal thật sự ghi xuống đĩa):**
```powershell
docker exec facility-db-1 psql -U smartpark_facility -d smartpark_facility -c "select physical_state, reservation_state, updated_at from parking_slots where id = '$($claim.id)';"
```

**PASS:** đúng chuỗi `401 → 200 → OCCUPIED → 204 → 204 → AVAILABLE/UNRESERVED/isBookable=true`. Release lần 2 **không** được trả 409/500 và **không** được để slot kẹt `OCCUPIED`.
Kết quả đã chạy lại 2026-11-03: `401` / `200 id=… code=CarParkingSpace_001` / `OCCUPIED` / `204` / `204` / `AVAILABLE / UNRESERVED / True`.

**FAIL:** `3.1` trả 200 (thiếu bảo vệ key — nghiêm trọng); `3.3` vẫn `AVAILABLE` (endpoint internal không ghi); release #2 trả 5xx.

**BLOCKED (đối với Postman):** nhánh **ledger** của consumer (`occupancy_release_ledger`) — chỉ ghi khi event có `slotId`; walk-in không có `slotId`, và Postman không publish được vào RabbitMQ. Nhánh này **đã được phủ bằng C4b** (integration test gọi thẳng `OccupancyProjectionStore`), nên đây không còn là lỗ hổng kiểm chứng — chỉ là giới hạn của công cụ Postman.

**Dọn dẹp:** sau C4 slot đã về `AVAILABLE`, không cần làm gì thêm. Nếu bỏ dở giữa chừng, chạy lại bước 4 một lần để nhả slot.

---

### C4b — Release-once tầng **ledger** (integration test, không dùng Postman)

**Kiểm gì:** cơ chế chống giải phóng slot hai lần của G3 nằm ở consumer, gồm 3 lớp bảo vệ nối tiếp trong `OccupancyProjectionStore.ApplyAsync`:

| Lớp | Bảng | Chặn được gì |
|---|---|---|
| 1 | `processed_events` | Cùng một `event_id` được giao lại 2 lần. |
| 2 | `occupancy_release_ledger` (UNIQUE `session_id`) | Hai `event_id` **khác nhau** nhưng cùng một phiên (retry sinh event mới). |
| 3 | `UPDATE ... WHERE physical_state='OCCUPIED'` | Slot đang không ở trạng thái bị chiếm. |

C4 (Postman) chỉ chạm lớp 3. C4b gọi thẳng `ApplyAsync` trên DB thật để chứng minh lớp 1 và **lớp 2** — lớp chưa từng chạy lần nào trước đây (`occupancy_release_ledger` luôn 0 dòng vì walk-in có `slotId = null`).

**Vì sao không làm được bằng Postman:** walk-in là loại session **duy nhất** tạo được qua REST trong worktree này; walk-in check-out phát `OccupancyReleased { sessionId, slotId: null }` ⇒ consumer bỏ qua cập nhật slot + ledger + watermark. Ngoài ra Postman không publish được vào RabbitMQ / không insert được vào Postgres.

**Điều kiện trước:**
- `facility-db-1` đang chạy (port 5442).
- Biến môi trường `SMARTPARK_STRUCTURE_TEST_CONNECTION` trỏ tới server Postgres **có quyền `CREATE DATABASE`** (user `smartpark_facility` trên `facility-db-1` có quyền này).
- Không set biến ⇒ 4 test **SKIP** (không fail) — nhờ attribute `[StructurePostgresFact]` dùng chung.

**Các bước:**

```powershell
# 1) Lấy mật khẩu từ .env (file git-ignored, không commit)
function GetEnvVar([string]$name){ ((Get-Content .\.env | Where-Object { $_ -match "^$name=" }) -replace "^$name=",'').Trim().Trim('"').Trim("'") }

# 2) Trỏ test vào server dev có quyền tạo DB
$env:SMARTPARK_STRUCTURE_TEST_CONNECTION = "Host=localhost;Port=5442;Database=smartpark_facility;Username=smartpark_facility;Password=$(GetEnvVar 'SMARTPARK_DEV_PASSWORD')"

# 3) Chạy riêng nhóm test của C4b
dotnet test .\tests\Services\SmartParking.FacilityService.Tests\SmartParking.FacilityService.Tests.csproj --filter "FullyQualifiedName~OccupancyProjectionStore" -v minimal
```

**Kết quả mong đợi:** `Passed: 4, Failed: 0, Skipped: 0`.

| Test | Kịch bản | Chứng minh |
|---|---|---|
| `DuplicateEventIdIsAppliedOnce` | Cùng `eventId` áp 2 lần | Lần 1 `true`, lần 2 `false`; `processed_events` = 1; slot `OCCUPIED` |
| `SpentReleaseLedgerDoesNotFreeASlotReoccupiedByAnotherSession` | Slot bị chiếm lại bởi phiên B, rồi **replay release của phiên A** | Slot **vẫn `OCCUPIED`**; ledger của A vẫn 1 dòng (đây là bug mà ledger sinh ra để chặn) |
| `ReleaseWithoutSlotIsRecordedButNotProjected` | `OccupancyReleased` với `slotId = null` | Trả `false`; `processed_events` có dòng; `occupancy_release_ledger` = 0; slot không đổi |
| `StaleSequenceIsIgnoredAfterTheWatermarkAdvances` | Event `seq 5` sau khi watermark = 10 | Trả `false`; slot giữ trạng thái của `seq 10`; watermark = 10 |

**Lưu ý về ngữ nghĩa:** `ApplyAsync` trả `true` nghĩa là “event có `slotId` và đã đi qua cửa kiểm tra”, **không** phải “đã đổi được dòng nào”. Ở kịch bản ledger đã tiêu, hàm vẫn trả `true` nhưng `UPDATE` khớp 0 dòng — vì vậy test assert **trạng thái DB**, không assert giá trị trả về.

**Dữ liệu test:** mỗi test tự tạo database `occupancy_projection_test_<guid>`, nạp DDL thật (`02-facility-service-db.sql` + `parking-tenants.sql` + `realtime-projection.sql`), seed 1 tenant/site/zone/slot rồi `DROP DATABASE ... WITH (FORCE)`. An toàn trên server dùng chung, nhưng nên trỏ vào server riêng nếu có.

**PASS:** `Passed: 4, Failed: 0`; sau khi chạy, `SELECT datname FROM pg_database WHERE datname LIKE 'occupancy_projection_test_%'` không còn dòng nào.

**FAIL:** test 2 để slot về `AVAILABLE` ⇒ ledger hỏng (mức độ: nghiêm trọng — có thể giải phóng slot của phiên khác); test 1 trả `true` ở lần 2 ⇒ dedup `processed_events` hỏng.

**Lưu ý khi chạy cả project test:** `dotnet test` toàn bộ cho `173 passed / 1 failed` — test fail là `PostgresStructureWorkflowTests.GateConnectionsLeaveATwoConnectorQueryPoolAvailable` (timeout 5 s trong `NpgsqlConnection.OpenAsync`), **fail cả khi chạy một mình** nên là lỗi có sẵn của môi trường, không liên quan SPARK-244. Ngoài ra, ở HEAD `2e94684e` project test **không build được** (`CS0535`: `FakeOperatorIdentity` chưa cài `AuthorizeSiteAsync` do SPARK-244 thêm vào interface); đã bổ sung method cho fake trong `ReservationGateUseCaseTests.cs` (+9 dòng, chỉ là test code).

---

### C5 — Zone-only reserved count (folder 04)

**Kiểm gì:** `data.metrics.zoneCapacities[]` có `zoneOnlyReservedCount` (kiểu số, không null) và bằng số slot `RESERVED` thực tế của zone đó; đồng thời `allocatedSlotCount` **bằng** `zoneOnlyReservedCount`. Đây là **xấp xỉ hiện tại** (OPEN D0-5): reader chưa có nguồn cấp phát theo zone nên cả hai cùng lấy số slot `RESERVED` của subtree.

**Điều kiện trước:** site có ít nhất 1 zone (DEMO-HQ hiện có `Zone A`); đã chạy khối chuẩn bị ở C4.

**Các bước**

**Cách A — Postman:** mở folder `04 - Zone-only reserved count`, **Send** `4.1` rồi `4.2`. Script in kết quả ra **Console** (Postman → Console): `Zones: Zone A onlyReserved=0 allocated=0`.

**Cách B — PowerShell** (gọi snapshot 1 lần rồi tự đối chiếu — chỉ cần 1 dòng cho mỗi zone):

```powershell
$d = (Invoke-RestMethod -Method Get -Uri "$BASE/api/parking-lots/$SITE/operational-layout" -Headers $ownerH).data
foreach ($z in $d.metrics.zoneCapacities) {
  $reserved = ($d.slots | Where-Object { $_.zoneCode -eq $z.zoneCode -and $_.reservationState -eq 'RESERVED' }).Count
  "zone=$($z.zoneCode) max=$($z.maxCapacity) occupied=$($z.occupiedCount) allocated=$($z.allocatedSlotCount) onlyReserved=$($z.zoneOnlyReservedCount) reservedThucTe=$reserved match=$($z.zoneOnlyReservedCount -eq $reserved -and $z.allocatedSlotCount -eq $z.zoneOnlyReservedCount)"
}
```

`match=True` là PASS. Kết quả đã chạy lại 2026-11-03: `zone=Zone A max=48 occupied=0 allocated=0 onlyReserved=0 reservedThucTe=0 match=True`.

**PASS:** mỗi zone có đủ `zoneId, zoneCode, floorCode, maxCapacity, occupiedCount, allocatedSlotCount, zoneOnlyReservedCount` kiểu số; `zoneOnlyReservedCount == số slot RESERVED của zone` và `allocatedSlotCount == zoneOnlyReservedCount`.
**FAIL:** field thiếu/null; lệch số so với `slots[]`.

**BLOCKED:** "zone-only reservation" thật (giữ chỗ theo zone mà không gán slot) — chưa có nguồn dữ liệu/route. Vì DB hiện tại có 0 slot `RESERVED`, phép so ở trên chỉ chứng minh **đúng shape + đúng công thức** (`0 == 0`), chưa tạo được ca `> 0`. Đừng coi là fail.

**Ghi chú quan sát:** `maxCapacity` lấy từ cấu hình zone (=48) còn `slots[]` có thể nhiều hơn do cấu trúc thêm bởi SPARK-238 — chỉ đối chiếu `occupiedCount` / `allocatedSlotCount` / `zoneOnlyReservedCount` với `slots[]`, **không** so `maxCapacity`.

---

### C6 — Operator scope (folder 05)

**Kiểm gì:** operator chỉ được dùng site được gán; site lạ bị chặn `403 OPERATOR_LOT_UNASSIGNED`; role driver không qua được scope operator.

**Điều kiện trước:** `operator@smartpark.local` đã được gán site DEMO-HQ (khi seed, script in `1 grant`); đã chạy khối chuẩn bị ở C4 (có `$opToken`, `$drvToken`).

**Các bước** — làm đủ 6 bước, đúng thứ tự

**Cách A — Postman:** folder `05 - Operator scope`, **Send** lần lượt `5.1 → 5.6`.

**Cách B — PowerShell:**

1. `5.1` site-scope của site **được gán** → `200`, đúng 1 tenant:
   ```powershell
   $sc = Invoke-RestMethod -Method Get -Uri "$USER/api/operators/site-scope?siteId=$SITE" -Headers $opH
   "$($sc.userId) | tenants=$($sc.tenantIds -join ',')"     # tenants = a0000000-0000-4000-8000-000000000001
   ```
2. `5.2` dùng `operatorToken` gọi **site lạ** qua OperationService → `403 OPERATOR_LOT_UNASSIGNED`. **Đây là case quan trọng nhất**: chứng minh OperationService map 403 của UserService thành mã này.
   ```powershell
   curl.exe -s -o - -w "`nHTTP %{http_code}`n" "$OPS/api/parking-sessions?siteId=$FOREIGN" -H "Authorization: Bearer $opToken"
   # {"success":false,"code":"OPERATOR_LOT_UNASSIGNED","message":"This parking site is not assigned to the current operator."}
   # HTTP 403
   ```
3. `5.3` gọi thẳng UserService với site lạ → `403` kèm envelope **`FORBIDDEN`** chung chung. Lưu ý: mã `OPERATOR_LOT_UNASSIGNED` **chỉ** do OperationService thêm (bước 2) — thấy `FORBIDDEN` ở đây là **đúng**, đừng coi là fail:
   ```powershell
   curl.exe -s -o - -w "`nHTTP %{http_code}`n" "$USER/api/operators/site-scope?siteId=$FOREIGN" -H "Authorization: Bearer $opToken"
   # {"success":false,"code":"FORBIDDEN","message":"You do not have permission to perform this action.","traceId":"..."}
   # HTTP 403
   ```
4. `5.4` dùng `driverToken` gọi site-scope của site được gán → `403` (route `[Authorize(Roles=operator)]`: token hợp lệ nhưng sai role):
   ```powershell
   curl.exe -s -o - -w "`nHTTP %{http_code}`n" "$USER/api/operators/site-scope?siteId=$SITE" -H "Authorization: Bearer $drvToken"        # HTTP 403
   ```
5. `5.5` operator **được gán** đọc `operational-layout` → `200` (không được 403):
   ```powershell
   (Invoke-RestMethod -Method Get -Uri "$BASE/api/parking-lots/$SITE/operational-layout" -Headers $opH).data.slots.Count   # > 0
   ```
6. `5.6` `driverToken` gọi route cũ `/operator-layout` → `403 FORBIDDEN`:
   ```powershell
   curl.exe -s -o - -w "`nHTTP %{http_code}`n" "$BASE/api/parking-lots/$SITE/operator-layout" -H "Authorization: Bearer $drvToken"
   # {"success":false,"code":"FORBIDDEN","message":"Explicit SLOT_OVERRIDE permission at this lot is required."}
   # HTTP 403
   ```

**PASS:** đúng 6 kết quả theo thứ tự: `200 (tenants=…0001)` / `403 OPERATOR_LOT_UNASSIGNED` / `403` / `403` / `200` / `403 FORBIDDEN`.
Kết quả đã chạy lại 2026-11-03: khớp cả 6.

**FAIL:** `5.2` trả 200 (hở scope — nghiêm trọng, phải báo ngay); `5.4` trả 200 (role gate hỏng); `5.5` trả 403 (operator không được gán ⇒ chạy lại `seed-demo-site.ps1` rồi `repair-demo-identity.ps1 -Apply`).

**BLOCKED:** hub **reject** khi connect bằng `foreignSiteId` — Postman không làm được WebSocket, và `negotiate` trả **200 cho mọi caller** (hub không có `[Authorize]`, check nằm trong `OnConnectedAsync`). Kiểm bằng C9 (snippet Console trong browser) hoặc C8.

---

### C7 — Latency proxy (folder 06)

**Kiểm gì:** ngân sách Q4 `p95 ≤ 2 s` là **dev-only**, và Postman/PowerShell chỉ đo được **proxy REST** (`write → REST read`), **không** đo được chặng `Operations → SignalR → client`.

**Điều kiện trước:** đã chạy khối chuẩn bị ở C4; `probePlate` phải **duy nhất mỗi lần chạy** (dùng plate cố định sẽ ăn `409 VEHICLE_ALREADY_INSIDE`).

**Các bước**

**Cách A — Postman:** folder `06 - Realtime delivery / latency`, **Send** `6.1 → 6.4`; đọc Console: `REST read latency=…ms`, `Operations REST write -> REST read elapsed=…ms`.

**Cách B — PowerShell** (dán cả khối, chạy 1 lần):

```powershell
# 6.1 đọc snapshot và đo thời gian
$sw=[Diagnostics.Stopwatch]::StartNew()
Invoke-WebRequest -Method Get -Uri "$BASE/api/parking-lots/$SITE/operational-layout" -Headers $ownerH -UseBasicParsing | Out-Null
$sw.Stop(); "6.1 layout read = $($sw.ElapsedMilliseconds) ms"

# 6.2 ghi mồi bằng walk-in (plate phải duy nhất)
$plate = 'SP244PROBE' + (Get-Random -Maximum 99999)
$w = Invoke-RestMethod -Method Post -Uri "$OPS/api/parking-sessions/walk-in/check-in?siteId=$SITE" -ContentType 'application/json' -Headers $opH `
     -Body (@{plateNumber=$plate;vehicleType='CAR'}|ConvertTo-Json -Compress)
"6.2 probe check-in  = 200 id=$($w.data.id) plate=$plate"

# 6.3 đọc lại danh sách session và đo write -> read
$t0=Get-Date
$rows=(Invoke-RestMethod -Method Get -Uri "$OPS/api/parking-sessions?siteId=$SITE" -Headers $opH).data
$ms=((Get-Date)-$t0).TotalMilliseconds
"6.3 write->read     = $([int]$ms) ms | thaySession=$([bool]($rows | Where-Object { $_.id -eq $w.data.id }))"

# 6.4 dọn dẹp (BẮT BUỘC - bỏ bước này DB sẽ còn session ACTIVE)
Invoke-RestMethod -Method Post -Uri "$OPS/api/parking-sessions/$($w.data.id)/walk-in/check-out?siteId=$SITE" -ContentType 'application/json' -Headers $opH `
  -Body (@{plateNumber=$plate}|ConvertTo-Json -Compress) | Out-Null
"6.4 cleanup        = done"
```

**PASS:** `6.1 ≤ 2000 ms`; `6.3 ≤ 2000 ms` và `thaySession=True`; `6.4` không lỗi (200, có `exitTime`).
Đã đo lại 2026-11-03: `6.1` = 246 ms (lần nguội đầu tiên; các lần sau ~29–36 ms); `6.3` = 64–204 ms.

**FAIL:** vượt 2000 ms liên tục (chạy lại 3 lần rồi mới kết luận), hoặc `6.4` lỗi / để lại session `ACTIVE` (rác dữ liệu — kiểm bằng SQL ở §1.5).

**BLOCKED:** p95 thật của đường `write → outbox → RabbitMQ → consumer → SignalR → client` — cần ≥20 mẫu qua WebSocket (C8/C10). Con số ở C7 **không** được dùng để tuyên bố đạt Q4.

**Dọn dẹp khi quên bước 6.4:** endpoint check-out **bắt buộc** header `Content-Type: application/json` **và** body `{"plateNumber":"<plate>"}` — gửi không body → `415`, gửi `{}` → `400`. Muốn đóng hàng loạt:
```powershell
(Invoke-RestMethod -Method Get -Uri "$OPS/api/parking-sessions?siteId=$SITE" -Headers $opH).data |
  Where-Object { $_.status -eq 'ACTIVE' -and $_.originalPlate -like 'SP244*' } |
  ForEach-Object {
    Invoke-RestMethod -Method Post -Uri "$OPS/api/parking-sessions/$($_.id)/walk-in/check-out?siteId=$SITE" -ContentType 'application/json' -Headers $opH `
      -Body (@{plateNumber=$_.originalPlate}|ConvertTo-Json -Compress) | Out-Null
    "closed $($_.originalPlate)"
  }
```

---

### C8 — FE realtime end-to-end (browser)

**Kiểm gì:** FE nối hub, nhận `SnapshotRequired` khi connect, nhận delta khi có event, và **chỉ refetch snapshot** (không tự sửa state cục bộ, không nhấp nháy).

**Điều kiện trước:**
- Mục 1.3 xanh, marker `watchOperatorLayout` = YES.
- Đăng nhập operator, chọn lot **DEMO-HQ** trong dropdown.
- Postman đã có `operatorToken` (chạy folder 00) và biến `claimedSlotId` (chạy 3.2) nếu muốn test delta OCCUPIED.

**Các bước:**
1. Mở `http://localhost:5173`, đăng nhập `operator@smartpark.local`. Chọn **DEMO-HQ**.
2. DevTools → **Network → WS**. Kỳ vọng thấy kết nối tới
   `ws://localhost:5055/ws/parking-lots/b0000000-0000-4000-8000-000000000001/layout?access_token=...` với status **101**.
3. Tab **Messages**: ngay sau khi nối phải có frame **`SnapshotRequired`** (một tham số `siteId`).
4. Đếm số request `operational-layout` xuất hiện trong Network quanh thời điểm connect: `SnapshotRequired` được xử lý bằng `guard.reset()` + **một** lần refetch.
5. Trong Postman chạy **2.1** (walk-in check-in). Quay lại DevTools:
   - Messages có frame **`OccupancyChanged`** với `eventId`, `eventType`, `occurredAt`, `sequenceNumber`, `siteId`, `payload.action = "checkin_walkin"`.
   - Network có **đúng 1** request `operational-layout` mới trong ~2 s.
6. Chạy **3.2** (claim). Kỳ vọng frame **`SlotStateChanged`** với `payload.slotId` + `payload.physicalState = "OCCUPIED"` ⇒ **1** lần refetch; danh sách slot hiển thị slot đó OCCUPIED sau khi refetch.
7. Chạy **3.4** (release). Kỳ vọng frame **`OccupancyReleased`** ⇒ **1** lần refetch; slot trở lại AVAILABLE.
8. Quan sát UI: **không** nhấp nháy, **không** có vòng lặp refetch liên tục, không có lỗi đỏ trong Console.
9. Dọn dẹp: chạy **2.4** và **3.5** (nếu chưa).

**PASS:**
- WS `101`; `SnapshotRequired` xuất hiện đúng 1 lần sau connect.
- Mỗi event ⇒ **đúng một** refetch snapshot (không 0, không 2+).
- Frame có đủ 6 khóa envelope; `slotId` nằm trong `payload`.
- UI phản ánh thay đổi sau refetch; không nhấp nháy.

**FAIL:**
- Không có WS nào được mở ⇒ sai `VITE_RESERVATION_API_BASE_URL`, hoặc lot không được chọn, hoặc hub không chạy.
- WS `401`/`403` ⇒ token rỗng hoặc operator không được gán site.
- Mỗi event gây **nhiều** refetch ⇒ guard không chặn trùng (kiểm tra `eventId` có bị dùng lại).
- FE tự sửa slot state từ `payload` thay vì refetch ⇒ sai kiến trúc (phải thấy request `operational-layout` đi kèm).
- Không có frame nào khi Postman tạo event ⇒ broadcaster/backplane không chạy (xem log `reservation-service`).

---

### C9 — Reconnect, token và negative scope

**Kiểm gì:** hub tự nối lại sau khi service restart và **lấy lại snapshot đầy đủ** (không ghép nửa trạng thái); token hết hạn được refresh; operator không thuộc site không nhận được event.

**Các bước:**
1. Đang ở C8 bước 2–3 (WS 101). Restart OperationService:
   ```powershell
   docker compose --env-file .env -f compose.app.yml restart reservation-service
   ```
2. Quan sát DevTools WS: kết nối cũ `closed`, kết nối mới xuất hiện sau ~2–10 s (retry 2 s khi `onclose`, 10 s khi start fail), status **101**.
3. Sau khi nối lại: phải có **`SnapshotRequired`** mới và **đúng 1** refetch snapshot (đây là điểm quan trọng: reconnect = snapshot đầy đủ).
4. Đổi lot trong dropdown sang lot khác rồi quay lại DEMO-HQ: hub cũ đóng, hub mới mở, `SnapshotRequired` + 1 refetch mỗi lần.
5. Token: để nguyên tab ~ vài phút hoặc gọi refresh; hub phải tiếp tục nhận event (dùng `ensureAccessToken`). Đăng xuất ⇒ WS đóng (`authExpiredEvent`), không reconnect.
6. Negative scope: dùng DevTools Console của tab đã đăng nhập operator để mở thử hub với site lạ:
   ```js
   const c = new signalR.HubConnectionBuilder()
     .withUrl("http://localhost:5055/ws/parking-lots/00000000-0000-4000-8000-00000000dead/layout",
              { accessTokenFactory: () => "<operator_token>" })
     .build();
   c.start().catch(e => console.log("EXPECTED:", e.message));
   ```
   Kỳ vọng: start fail với thông điệp chứa **`OPERATOR_LOT_UNASSIGNED`**.

**PASS:** reconnect tự động + đúng 1 snapshot; đổi lot sạch; logout dừng hub; negative scope trả `OPERATOR_LOT_UNASSIGNED`.

**FAIL:** reconnect xong không refetch (client giữ state cũ); refetch lặp vô hạn sau reconnect; negative scope **kết nối thành công** (hở bảo mật — báo ngay).

**BLOCKED:** nếu Console không có `signalR` toàn cục (Vite bundle), dùng Postman/`wscat` không được ⇒ ghi nhận BLOCKED và dựa vào REST proxy `5.2` cho cùng mapping.

---

### C10 — Xác nhận các mục BLOCKED (không phải regression)

**Kiểm gì:** các khoảng trống đã biết vẫn đúng là "chưa có", tester không báo nhầm thành fail của SPARK-244.

**Các bước & kỳ vọng:**

1. **Duplicate/replay event** — bơm cùng một `eventId` 2 lần vào `smartpark.occupancy`.
   - **PASS:** `processed_events` không tăng thêm; `parking_slots` không đổi.
   - Không làm được bằng Postman ⇒ **BLOCKED**, không phải FAIL.

2. **Stale sequence** — bơm event có `sequenceNumber` nhỏ hơn watermark của slot.
   - **PASS:** bị bỏ qua (không hồi sinh slot đã AVAILABLE). **BLOCKED** tương tự.

3. **Consumer release-once ledger** — event `OccupancyReleased` có `slotId`.
   - **PASS:** `occupancy_release_ledger` +1 dòng/session; release lặp lại không giải phóng slot của session khác.
   - Cần seed reservation (không có route tạo reservation) ⇒ **BLOCKED**.

4. **Hub reject unassigned** — `negotiate` **luôn 200** (kể cả anonymous).
   - **PASS khi hiểu đúng:** đây là thiết kế; reject xảy ra ở handshake WebSocket. Đo bằng C9 bước 6.
   - **FAIL khi:** coi `negotiate` 200 là "hub hở quyền".

5. **Redis multi-instance fan-out** — cần **2** instance OperationService.
   - **PASS:** event tạo ở instance A đến được client nối instance B. Hiện chỉ 1 instance ⇒ **BLOCKED**.

6. **p95 ≤ 2 s đầy đủ** — cần ≥20 mẫu qua WebSocket ⇒ **BLOCKED**.

7. **G1 requested vs allocated** (`approachingReservation`) — chưa implement.
   - **PASS:** `slots[].operatorContext` luôn `null`, `sessionType` luôn `null`.
   - **FAIL khi:** kỳ vọng có `requestedSlotCode`/`allocatedSlotCode` rồi báo bug.

---

## 5. Những "fail" KHÔNG phải bug của SPARK-244

| Hiện tượng | Lý do (đã xác minh) |
|---|---|
| `negotiate` trả **200** cho anonymous và cho site lạ | Hub không có `[Authorize]`; OperationService không đăng ký JWT auth. Check scope nằm trong `ParkingLotLayoutHub.OnConnectedAsync`, chỉ chạy ở handshake WS. |
| `occupancy_release_ledger` và `slot_sequence_watermarks` = **0** dù consumer chạy | Walk-in event có `slotId = null`. Ledger ghi theo session/slot, watermark theo slot ⇒ không có gì để ghi. Đúng thiết kế G3/G5. |
| Snapshot **không** có `eventId`/`sequenceNumber`/`siteId` ở từng slot | Đúng G5: ba field nằm trên `RealtimeEventEnvelope`, không nằm trên snapshot. OpenAPI đã được sửa cho khớp C# DTO. |
| `zoneOnlyReservedCount == allocatedSlotCount` | Xấp xỉ hiện tại (OPEN D0-5): cả hai = số slot RESERVED của subtree. Chưa có nguồn allocation theo zone. |
| Docs ghi "48 slot" nhưng DB có **71** `parking_slots` | Cấu trúc do SPARK-238 (nhiều unit/zone) seed thêm. Không phải lỗi; đối chiếu theo `slots[]` thực tế của response. |
| `operatorContext` luôn `null` | G1 (requested vs allocated) chưa implement — nằm ngoài MVP của task này. |
| Outbox không có `aggregate_type`/`aggregate_id`/`retry_count` | Thiết kế §9.3 đề xuất, implementation dùng `event_id` PK + `published_at` + `sequence_number`. Không ảnh hưởng hành vi. |
| `npx tsc --noEmit` ra **4 lỗi** | Pre-existing, không liên quan: `GroupedNumberInput.tsx(168,7)` TS17001, `OperatorDashboard.tsx` TS2304 `SlotStatusFilter`, `SiteManagement.tsx(61,28)` TS2552 `setMessage`, `useSiteManagement.ts(93,42)` TS2339 `reassignSite`. Sau khi nối hub vẫn đúng 4. |
| `OccupancyChanged` không có `slotId` | Walk-in không gán slot (đúng); check-in reservation mới phát `SlotStateChanged` có `slotId`. |
| Sequence là `max(site)+1`, không theo slot | Đúng implementation; consumer watermark theo slot. Rủi ro đua đã ghi nhận (xem §7 D3). |

---

## 6. Test tự động — lệnh chính xác

### 6.1. Backend (build)

```powershell
dotnet build src/Services/UserService/SmartParking.UserService.API/SmartParking.UserService.API.csproj --nologo -m:1
dotnet build src/Services/FacilityService/SmartParking.FacilityService.API/SmartParking.FacilityService.API.csproj --nologo -m:1
dotnet build src/Services/OperationService/SmartParking.OperationService.API/SmartParking.OperationService.API.csproj --nologo -m:1
```

Kỳ vọng đã ghi nhận: UserService `0W/0E`; FacilityService `0W/0E`; OperationService `0E` + **2 warning `NU1902` (MessagePack)** là pre-existing, không phải lỗi của task.

### 6.2. Postman collection bằng Newman (không cần mở Postman)

`newman` chưa cài global; dùng `npx`. Không in secret ra màn hình/commit:

```powershell
function GetEnvValue($name) {
  $line = (Select-String -Path .env -Pattern "^$name=").Line
  if (-not $line) { throw "Missing $name in .env" }
  ($line -split '=',2)[1].Trim().Trim('"').Trim("'")
}

$ownerPw = GetEnvValue 'SMARTPARK_OWNER_PASSWORD'
$opPw    = GetEnvValue 'SMARTPARK_OPERATOR_PASSWORD'
$adminPw = GetEnvValue 'SMARTPARK_ADMIN_PASSWORD'
$svcKey  = GetEnvValue 'SMARTPARK_SERVICE_KEY'

npx --yes newman run docs/04-testing/postman/SmartParking-Realtime-Layout-SPARK-244.postman_collection.json `
  -e docs/04-testing/postman/SmartParking-Realtime-Layout-SPARK-244.postman_environment.json `
  --env-var "ownerPassword=$ownerPw" --env-var "operatorPassword=$opPw" `
  --env-var "adminPassword=$adminPw" --env-var "serviceKey=$svcKey" `
  --reporters cli --timeout-request 15000
```

Kỳ vọng: `requests 33/33`, `test-scripts 33/33`, `assertions 101/101`, `failed 0`, exit code 0.

### 6.3. Frontend

```powershell
Set-Location FE
npx tsc --noEmit      # chỉ được có đúng 4 lỗi pre-existing ở §5
npx vite build        # EXIT=0
Set-Location ..
```

---

## 7. Checklist đóng gói (trước khi merge `develop`)

| # | Việc | Trạng thái |
|---|---|---|
| D1 | Commit 3 file docs đang sửa (session log + 2 guide này) | chưa |
| D2 | Push + PR `feat/SPARK-244-realtime-monitoring` → `develop` | chưa |
| D3 | Ghi nợ: đua cấp `sequence_number` (`max(site)+1`, index `(site_id, sequence_number)` **không** unique) ⇒ có thể trùng khi 2 check-in đồng thời cùng site | chưa |
| D4 | Ghi nợ: G1 requested-vs-allocated (`operatorContext`) chưa implement | chưa |
| D5 | Ghi nợ: G4 zone-only xấp xỉ (OPEN D0-5) | chưa |
| D6 | Ghi nợ: chưa có harness p95 + chưa test Redis 2 instance | chưa |

Thứ tự merge: **238 → develop** (đã merge vào nhánh này), sau đó **244 → develop**.

---

## Phụ lục A — Bảng tra cứu API nhanh

Route `/api/**` cần `Authorization: Bearer <token>`. Route `/internal/**` cần `X-Service-Key`.

| Method + path | Cổng | Body / header chính | Kỳ vọng |
|---|---|---|---|
| `POST /api/auth/login` | 5035 | `{ email, password }` | 200, `data.accessToken`, `data.user.role` |
| `GET /api/operators/site-scope?siteId=` | 5035 | Bearer operator | 200 `{userId, tenantIds[]}`; 403 nếu không gán |
| `GET /api/parking-lots/{siteId}/operational-layout` | 5045 | — (anonymous OK) | 200 snapshot sanitized; 400 `INVALID_VEHICLE_TYPE` / `INVALID_FLOOR`; 404 `SITE_NOT_FOUND` |
| `GET /api/parking-lots/{siteId}/operator-layout` | 5045 | Bearer operator | 200; driver → 403 `FORBIDDEN` |
| `POST /internal/gate-sites/{siteId}/slots/claim` | 5045 | `X-Service-Key`; `{tenantId, vehicleType}` | 200 `{id,code,vehicleType}`; 401 thiếu key; 409 `NO_AVAILABLE_SLOT` |
| `POST /internal/gate-sites/{siteId}/slots/{slotId}/release` | 5045 | `X-Service-Key` | 204 (kể cả lần 2) |
| `POST /api/parking-sessions/walk-in/check-in?siteId=` | 5055 | `{licensePlate, vehicleType}` | 200 `data.id`, `slotId=null`; 409 `VEHICLE_ALREADY_INSIDE` |
| `GET /api/parking-sessions?siteId=` | 5055 | Bearer operator | 200 `data[]` |
| `POST /api/parking-sessions/{id}/walk-in/check-out?siteId=` | 5055 | **`Content-Type: application/json`** + `{plateNumber}` | 200 `data.exitTime`; 415 nếu thiếu content-type; 400 nếu thiếu plate |
| `WS /ws/parking-lots/{siteId}/layout` | 5055 | `?access_token=` hoặc header `Authorization` | 101 + `SnapshotRequired`; không token → `UNAUTHENTICATED`; site lạ → `OPERATOR_LOT_UNASSIGNED` |

Sự kiện hub (tên method = `eventType`), envelope chung `{eventId, eventType, occurredAt, sequenceNumber, siteId, payload}`:

| Event | `payload` | Phát khi |
|---|---|---|
| `SlotStateChanged` | `{reservationId, sessionId, slotId, physicalState}` | check-in reservation / claim có slot |
| `OccupancyReleased` | `{sessionId, slotId}` | check-out / release |
| `OccupancyChanged` | `{sessionId, action:"checkin_walkin"}` | walk-in (không có `slotId`) |
| `SnapshotRequired` | `siteId` | ngay sau khi client join group |

---

## Phụ lục B — Mẫu kiểm tra bằng curl.exe / SQL

PowerShell 5.1 không có `-SkipHttpErrorCheck`, nên dùng `curl.exe` khi cần thấy mã lỗi:

```powershell
# Snapshot (200)
curl.exe -s -w "`nHTTP %{http_code}`n" `
  "http://localhost:5045/api/parking-lots/b0000000-0000-4000-8000-000000000001/operational-layout"

# Scope site lạ (mong đợi 403 OPERATOR_LOT_UNASSIGNED)
curl.exe -s -w "`nHTTP %{http_code}`n" `
  "http://localhost:5055/api/parking-sessions?siteId=00000000-0000-4000-8000-00000000dead" `
  -H "Authorization: Bearer $token"

# Claim (mong đợi 200) / thiếu key (mong đợi 401)
$tenantId = 'a0000000-0000-4000-8000-000000000001'
$key = ((Select-String -Path .env -Pattern '^SMARTPARK_SERVICE_KEY=').Line -split '=',2)[1].Trim()
$claim = @{ tenantId = $tenantId; vehicleType = 'CAR' } | ConvertTo-Json
curl.exe -s -w "`nHTTP %{http_code}`n" -X POST `
  "http://localhost:5045/internal/gate-sites/b0000000-0000-4000-8000-000000000001/slots/claim" `
  -H "Content-Type: application/json" -H "X-Service-Key: $key" -d $claim
```

```powershell
# Outbox: còn event chưa publish?
docker exec operations-db-1 psql -w -U smartpark_operations -d smartpark_operations -c `
"select count(*) filter (where published_at is null) as pending, count(*) as total from outbox_events;"

# Outbox theo loại
docker exec operations-db-1 psql -w -U smartpark_operations -d smartpark_operations -c `
"select event_type, count(*), min(sequence_number), max(sequence_number) from outbox_events group by 1 order by 1;"

# Consumer: đã áp dụng đủ chưa + 3 bảng phụ
docker exec facility-db-1 psql -w -U smartpark_facility -d smartpark_facility -c `
"select (select count(*) from processed_events) processed,
        (select count(*) from slot_sequence_watermarks) watermarks,
        (select count(*) from occupancy_release_ledger) ledger;"

# Slot đang bị chiếm
docker exec facility-db-1 psql -w -U smartpark_facility -d smartpark_facility -c `
"select physical_state, reservation_state, count(*) from parking_slots group by 1,2 order by 3 desc;"
```

---

## 8. Nguồn bằng chứng

| Chủ đề | Đường dẫn |
|---|---|
| Hub / Redis backplane / broadcaster | `src/Services/OperationService/SmartParking.OperationService.API/Program.cs`, `API/Hubs/ParkingLotLayoutHub.cs`, `API/Realtime/SignalRLayoutRealtimeBroadcaster.cs` |
| Map lỗi scope 401/403 | `.../Infrastructure/Clients/OperatorIdentityClient.cs` |
| Outbox worker / publisher / DI | `.../Infrastructure/Outbox/OccupancyOutboxWorker.cs`, `RabbitMqOutboxPublisher.cs`, `.../InfrastructureDependencyInjection.cs` |
| Ghi outbox + cấp sequence | `.../Persistence/ReservationGateStore.cs` |
| Consumer + idempotency | `FacilityService/Infrastructure/OccupancyProjectionWorker.cs`, `FacilityService/Persistence/OccupancyProjectionStore.cs` |
| Snapshot DTO + zone field | `FacilityService/Application/OperationalLayoutProjection.cs` |
| Snapshot reader | `FacilityService/Persistence/PostgresOperationalLayoutReader.cs` |
| Claim / release nội bộ | `FacilityService/API/Program.cs`, `Persistence/OperatorGateSlotStore.cs` |
| Scope route | `UserService/API/Controllers/OperatorsController.cs` |
| SQL overlay | `scripts/database/overlay/realtime-outbox.sql`, `realtime-projection.sql` |
| Áp overlay | `scripts/apply-workflow-migrations.ps1` (`-Service Parking` / `-Service Operation -SchemaRoot .`) |
| FE realtime | `FE/src/lib/operatorLayoutLive.ts`, `FE/src/lib/realtimeIdempotency.ts`, `FE/src/roles/operator/dashboard/OperatorDashboard.tsx` |
| Design | `docs/03-design/api/3d-digital-twin-spark-244-delta-design.md`, `3d-digital-twin-openapi.yaml` |
| Session log | `ai/tasks/active/SESSION-20261028-spark-244.md` |